using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;
using M2Hub.Desktop.Services.Okey;

namespace M2Hub.Desktop.ViewModels;

/// Okey-Helfer als Session-Tracker: Du setzt deine 5 offenen Karten, der Helfer
/// markiert die beste Kombo gruen (werten) bzw. die wegzuwerfende Karte rot
/// (wenn keine Kombo da ist), zaehlt die Punkte, zeigt die aktuelle Truhe und
/// schaetzt live die Chance auf Gold/Silber ab dem jetzigen Stand.
public sealed class OkeyHelperViewModel : ViewModelBase
{
    private const int SimRuns = 60_000;

    private List<OkeyCardVM>? _comboCards;
    private int _comboScore;
    private int _simToken;

    public OkeyHelperViewModel()
    {
        for (var color = 0; color < OkeyCalc.Colors; color++)
            for (var n = 1; n <= OkeyCalc.Numbers; n++)
                Palette.Add(new OkeyCardVM(n, color));

        ClickHandCommand = new RelayCommand(p => PlayOrDiscard(p as OkeyCardVM));
        ClickPaletteCommand = new RelayCommand(p => TogglePalette(p as OkeyCardVM));
        NewSetCommand = new RelayCommand(_ => NewSet());
        ToggleHelpCommand = new RelayCommand(_ => IsHelpOpen = !IsHelpOpen);

        _helpTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        _helpTimer.Tick += (_, _) => AdvanceExamples();
        AdvanceExamples();

        ToggleLootCommand = new RelayCommand(_ => IsLootOpen = !IsLootOpen);
        SelectSeasonCommand = new RelayCommand(p => { if (p is LootSeasonVM s) SelectSeason(s); });
        SelectChestCommand = new RelayCommand(p => { if (p is LootChestVM c) SelectedChest = c; });
        BuildLoot();

        Recompute();
    }

    /* ---------- Loot der Truhen ---------- */

    public ObservableCollection<LootSeasonVM> LootSeasons { get; } = new();
    public ObservableCollection<LootChestVM> SelectedChests { get; } = new();
    public bool HasLoot => LootSeasons.Count > 0;

    private bool _isLootOpen;
    public bool IsLootOpen { get => _isLootOpen; set => Set(ref _isLootOpen, value); }

    public RelayCommand ToggleLootCommand { get; }
    public RelayCommand SelectSeasonCommand { get; }
    public RelayCommand SelectChestCommand { get; }

    private LootChestVM? _selectedChest;
    /// Die aufgeklappte Truhe - ihre Items stehen in der rechten Sidebar.
    public LootChestVM? SelectedChest
    {
        get => _selectedChest;
        private set
        {
            if (!Set(ref _selectedChest, value)) return;
            foreach (var season in LootSeasons)
                foreach (var chest in season.Chests)
                    chest.IsOpen = ReferenceEquals(chest, value);
            Raise(nameof(HasSelectedChest));
        }
    }

    public bool HasSelectedChest => _selectedChest is not null;

    private void BuildLoot()
    {
        foreach (var s in OkeyLoot.Data.Seasons) LootSeasons.Add(new LootSeasonVM(s));
        Raise(nameof(HasLoot));
        if (LootSeasons.Count > 0) SelectSeason(LootSeasons[0]);
    }

    private void SelectSeason(LootSeasonVM season)
    {
        foreach (var s in LootSeasons) s.IsActive = ReferenceEquals(s, season);
        SelectedChests.Clear();
        foreach (var c in season.Chests) SelectedChests.Add(c);
        SelectedChest = null;   // Sidebar leeren, bis eine Truhe gewaehlt wird
    }

    public ObservableCollection<OkeyCardVM> Palette { get; } = new();
    public ObservableCollection<OkeyCardVM> Hand { get; } = new();

    public RelayCommand ClickHandCommand { get; }
    public RelayCommand ClickPaletteCommand { get; }
    public RelayCommand NewSetCommand { get; }
    public RelayCommand ToggleHelpCommand { get; }

    /* ---------- Hilfe (Event-Erklaerung, animierte Beispiele) ---------- */

    private readonly DispatcherTimer _helpTimer;
    private int _helpStep;
    private bool _isHelpOpen;

    public bool IsHelpOpen
    {
        get => _isHelpOpen;
        set
        {
            if (!Set(ref _isHelpOpen, value)) return;
            if (value) _helpTimer.Start(); else _helpTimer.Stop();
        }
    }

    /// Drei animierte Beispielkombos: Set, gleichfarbige Reihe, gemischte
    /// Reihe. Sie wechseln die Zahl/Farbe durch, sodass die Punkte je Kombo
    /// kompakt an einem Beispiel sichtbar werden.
    public OkeyExample ExampleSet { get; } = new();
    public OkeyExample ExampleRowSame { get; } = new();
    public OkeyExample ExampleRowMixed { get; } = new();

    private void AdvanceExamples()
    {
        var step = _helpStep++;

        // Set: alle drei dieselbe Zahl; Farbe wechselt (fuer die Punkte egal).
        var n = step % OkeyCalc.Numbers + 1;
        var sc = step % OkeyCalc.Colors;
        ExampleSet.SetCards(n, sc, n, sc, n, sc);
        ExampleSet.PointsText = Loc.T("okey.points", 10 * (n + 1));

        // Gleichfarbige Reihe: s, s+1, s+2 in einer wechselnden Farbe.
        var s = step % 6 + 1;
        var rc = step % OkeyCalc.Colors;
        ExampleRowSame.SetCards(s, rc, s + 1, rc, s + 2, rc);
        ExampleRowSame.PointsText = Loc.T("okey.points", 40 + 10 * s);

        // Gemischte Reihe: s, s+1, s+2, drei verschiedene Farben (Permutation).
        ExampleRowMixed.SetCards(
            s, step % 3, s + 1, (step + 1) % 3, s + 2, (step + 2) % 3);
        ExampleRowMixed.PointsText = Loc.T("okey.points", 10 * s);
    }

    /* ---------- Punkte / Truhe ---------- */

    private int _points;
    public int Points { get => _points; private set { if (Set(ref _points, value)) { Raise(nameof(PointsText)); Raise(nameof(ChestText)); Raise(nameof(ChestBrush)); Raise(nameof(ChestImage)); } } }

    /// Truhen-Icon zum aktuellen Punktestand (Bronze/Silber/Gold).
    public Bitmap? ChestImage =>
        OkeyLoot.ChestIconForTier(_points >= OkeyCalc.Gold ? 2 : _points >= OkeyCalc.Silver ? 1 : 0);

    public string PointsText => Loc.T("okey.points", _points);

    public string ChestText => _points >= OkeyCalc.Gold
        ? Loc.T("okey.chestGold")
        : _points >= OkeyCalc.Silver ? Loc.T("okey.chestSilver") : Loc.T("okey.chestBronze");

    public IBrush ChestBrush => _points >= OkeyCalc.Gold
        ? GoldBrush
        : _points >= OkeyCalc.Silver ? SilverBrush : BronzeBrush;

    private static readonly IBrush GoldBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#e0a92e"));
    private static readonly IBrush SilverBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#c7ccd6"));
    private static readonly IBrush BronzeBrush = new SolidColorBrush(Avalonia.Media.Color.Parse("#b06a3a"));

    /* ---------- Rat ---------- */

    private string _adviceText = "";
    public string AdviceText { get => _adviceText; private set => Set(ref _adviceText, value); }

    private bool _onlyBronze;
    /// Wahr, wenn mit den noch vorhandenen Karten Silber/Gold nicht mehr
    /// erreichbar ist - dann lohnt Weiterspielen nicht, man kann beenden.
    public bool OnlyBronze { get => _onlyBronze; private set => Set(ref _onlyBronze, value); }

    public string EndHintText => Loc.T("okey.endHint");

    private void Recompute()
    {
        foreach (var c in Hand) c.Recommend = 0;
        _comboCards = null;
        _comboScore = 0;

        // Ist ueberhaupt noch Silber erreichbar? Obergrenze: je 3 noch
        // vorhandene Karten eine Kombo, hoechstens die beste noch bildbare.
        var available = Palette.Where(c => !c.Used).Select(c => new OkeyCard(c.Number, c.Color)).ToList();
        var maxAdd = available.Count / 3 * OkeyCalc.BestSingleComboValue(available);
        OnlyBronze = _points + maxAdd < OkeyCalc.Silver;

        if (Hand.Count >= 3)
        {
            var hand = Hand.Select(c => new OkeyCard(c.Number, c.Color)).ToList();
            var best = OkeyCalc.BestCombo(hand);
            if (best.Score > 0)
            {
                _comboCards = [Hand[best.A], Hand[best.B], Hand[best.C]];
                _comboScore = best.Score;
                foreach (var c in _comboCards) c.Recommend = 1;   // gruen = werten
                AdviceText = Loc.T("okey.adviceTake", best.Score);
                UpdateSim();
                return;
            }
        }

        if (Hand.Count >= 5)
        {
            var hand = Hand.Select(c => new OkeyCard(c.Number, c.Color)).ToList();
            var idx = OkeyCalc.BestDiscardIndex(hand, Present());
            Hand[idx].Recommend = 2;   // rot = wegwerfen
            AdviceText = Loc.T("okey.adviceToss");
        }
        else
        {
            AdviceText = Loc.T("okey.adviceFill");
        }

        UpdateSim();
    }

    private void PlayOrDiscard(OkeyCardVM? card)
    {
        if (card is null || !Hand.Contains(card)) return;

        if (_comboCards is not null)
        {
            // Nur die grün markierte Kombo lässt sich werten.
            if (!_comboCards.Contains(card)) return;
            Points += _comboScore;
            foreach (var c in _comboCards) { c.Used = true; c.InHand = false; Hand.Remove(c); }
        }
        else
        {
            // Keine Kombo: angeklickte Karte wegwerfen (verbraucht).
            card.Used = true; card.InHand = false; Hand.Remove(card);
        }

        RaiseHand();
        Recompute();
    }

    private void TogglePalette(OkeyCardVM? card)
    {
        if (card is null || card.Used) return;
        if (card.InHand) { card.InHand = false; Hand.Remove(card); }
        else if (Hand.Count < 5) { card.InHand = true; Hand.Add(card); }
        else return;

        RaiseHand();
        Recompute();
    }

    private void NewSet()
    {
        foreach (var c in Palette) { c.InHand = false; c.Used = false; c.Recommend = 0; }
        Hand.Clear();
        Points = 0;
        RaiseHand();
        Recompute();
    }

    public bool HandFull => Hand.Count >= 5;
    public string HandCountText => Loc.T("okey.handCount", Hand.Count);

    private void RaiseHand()
    {
        Raise(nameof(HandFull));
        Raise(nameof(HandCountText));
    }

    /// Welche Karten noch im Spiel sind (nicht verbraucht) - fuer Wegwurf und
    /// Simulation.
    private bool[,] Present()
    {
        var grid = new bool[OkeyCalc.Numbers, OkeyCalc.Colors];
        foreach (var c in Palette) if (!c.Used) grid[c.Number - 1, c.Color] = true;
        return grid;
    }

    /* ---------- Live-Planer ---------- */

    private string _goldText = "–", _silverText = "–", _bronzeText = "–", _meanText = "–";
    public string GoldText { get => _goldText; private set => Set(ref _goldText, value); }
    public string SilverText { get => _silverText; private set => Set(ref _silverText, value); }
    public string BronzeText { get => _bronzeText; private set => Set(ref _bronzeText, value); }
    public string MeanText { get => _meanText; private set => Set(ref _meanText, value); }

    public string OrientationText => Loc.T("okey.orientation", OkeyCalc.AllSets, OkeyCalc.TheoreticalMax);

    private async void UpdateSim()
    {
        var token = ++_simToken;
        var points = _points;
        var startHand = Hand.Select(c => new OkeyCard(c.Number, c.Color)).ToList();
        var deck = Palette.Where(c => !c.Used && !c.InHand).Select(c => new OkeyCard(c.Number, c.Color)).ToList();

        var stats = await Task.Run(() => OkeyCalc.Simulate(SimRuns, points, startHand, deck)).ConfigureAwait(true);
        if (token != _simToken) return;   // ein neuerer Lauf ist schon unterwegs

        GoldText = $"{stats.PGold * 100:0.0} %";
        SilverText = $"{stats.PSilver * 100:0.0} %";
        BronzeText = $"{stats.PBronze * 100:0.0} %";
        MeanText = Loc.T("okey.points", (int)System.Math.Round(stats.Mean));
    }

    public void RelabelAfterLanguageChange()
    {
        Raise(nameof(PointsText));
        Raise(nameof(ChestText));
        Raise(nameof(HandCountText));
        Raise(nameof(OrientationText));
        Raise(nameof(EndHintText));
        Recompute();
    }
}

/// Eine Karte in Palette/Hand: Zahl 1-8, Farbe 0-2, mit Zustand und
/// Anzeigefarben.
public sealed class OkeyCardVM : ViewModelBase
{
    private static readonly IBrush[] Fills =
    [
        new SolidColorBrush(Avalonia.Media.Color.Parse("#d64545")), // Rot
        new SolidColorBrush(Avalonia.Media.Color.Parse("#3b82f6")), // Blau
        new SolidColorBrush(Avalonia.Media.Color.Parse("#e0a92e")), // Gelb
    ];

    private static readonly IBrush[] Texts =
    [
        Brushes.White, Brushes.White, new SolidColorBrush(Avalonia.Media.Color.Parse("#1a1206")),
    ];

    private bool _inHand;
    private bool _used;
    private int _reco;

    public OkeyCardVM(int number, int color)
    {
        Number = number;
        Color = color;
    }

    public int Number { get; }
    public int Color { get; }

    public bool InHand { get => _inHand; set => Set(ref _inHand, value); }
    public bool Used { get => _used; set => Set(ref _used, value); }

    /// 0 = keine Empfehlung, 1 = werten (gruen), 2 = wegwerfen (rot).
    public int Recommend
    {
        get => _reco;
        set { if (Set(ref _reco, value)) { Raise(nameof(IsTake)); Raise(nameof(IsToss)); } }
    }

    public bool IsTake => _reco == 1;
    public bool IsToss => _reco == 2;

    public IBrush Fill => Fills[Color];
    public IBrush Foreground => Texts[Color];
}

/// Die Kartenfarben an einer Stelle (auch fuer die animierten Beispiele).
public static class OkeyColors
{
    private static readonly IBrush[] Fills =
    [
        new SolidColorBrush(Avalonia.Media.Color.Parse("#d64545")),
        new SolidColorBrush(Avalonia.Media.Color.Parse("#3b82f6")),
        new SolidColorBrush(Avalonia.Media.Color.Parse("#e0a92e")),
    ];

    private static readonly IBrush[] Texts =
    [
        Brushes.White, Brushes.White, new SolidColorBrush(Avalonia.Media.Color.Parse("#1a1206")),
    ];

    public static IBrush Fill(int color) => Fills[((color % 3) + 3) % 3];
    public static IBrush Text(int color) => Texts[((color % 3) + 3) % 3];
}

/// Eine Beispielkarte in der Hilfe - Zahl und Farbe wechseln animiert.
public sealed class OkeyExampleCard : ViewModelBase
{
    private int _number = 1;
    private int _color;

    public int Number { get => _number; set => Set(ref _number, value); }

    public int Color
    {
        get => _color;
        set { if (Set(ref _color, value)) { Raise(nameof(Fill)); Raise(nameof(Foreground)); } }
    }

    public IBrush Fill => OkeyColors.Fill(_color);
    public IBrush Foreground => OkeyColors.Text(_color);
}

/// Ein animiertes Beispiel: drei Karten plus die Punkte dafuer.
public sealed class OkeyExample : ViewModelBase
{
    public OkeyExample()
    {
        for (var i = 0; i < 3; i++) Cards.Add(new OkeyExampleCard());
    }

    public ObservableCollection<OkeyExampleCard> Cards { get; } = new();

    private string _pointsText = "";
    public string PointsText { get => _pointsText; set => Set(ref _pointsText, value); }

    public void SetCards(int n1, int c1, int n2, int c2, int n3, int c3)
    {
        Cards[0].Number = n1; Cards[0].Color = c1;
        Cards[1].Number = n2; Cards[1].Color = c2;
        Cards[2].Number = n3; Cards[2].Color = c3;
    }
}

/// Eine Saison (Sommer/Winter) in der Loot-Ansicht.
public sealed class LootSeasonVM : ViewModelBase
{
    private bool _active;

    public LootSeasonVM(LootSeason data)
    {
        Key = data.Key;
        Name = data.Name;
        Chests = data.Chests.OrderBy(c => c.Tier).Select(c => new LootChestVM(c)).ToList();
    }

    public string Key { get; }
    public string Name { get; }
    public IReadOnlyList<LootChestVM> Chests { get; }

    public bool IsActive { get => _active; set => Set(ref _active, value); }
}

/// Eine Truhe mit ihrem Loot.
public sealed class LootChestVM : ViewModelBase
{
    private static readonly IBrush[] TierBrushes =
    [
        new SolidColorBrush(Avalonia.Media.Color.Parse("#b06a3a")), // Bronze
        new SolidColorBrush(Avalonia.Media.Color.Parse("#c7ccd6")), // Silber
        new SolidColorBrush(Avalonia.Media.Color.Parse("#e0a92e")), // Gold
    ];

    private bool _open;

    public LootChestVM(LootChest data)
    {
        Name = data.Name;
        Tier = data.Tier;
        Icon = OkeyLoot.ChestIcon(data.Key);
        // Nach Seltenheit: kleinste Wahrscheinlichkeit zuerst (bestes/rarstes Item oben).
        Items = data.Items.Select(i => new LootItemVM(i)).OrderBy(i => i.ChanceValue).ToList();
    }

    public string Name { get; }
    public int Tier { get; }
    public Bitmap? Icon { get; }
    public IBrush TierBrush => TierBrushes[Math.Clamp(Tier, 0, 2)];
    public IReadOnlyList<LootItemVM> Items { get; }

    /// Ob diese Truhe gerade aufgeklappt ist (Sidebar zeigt ihre Items).
    public bool IsOpen { get => _open; set => Set(ref _open, value); }
}

/// Ein Loot-Item mit Bild und Wahrscheinlichkeit.
public sealed class LootItemVM
{
    public LootItemVM(LootItemData data)
    {
        Name = data.Name;
        Chance = data.Chance;
        Image = OkeyLoot.Image(data.File);

        var t = (data.Chance ?? "").Replace("%", "").Replace(",", ".").Trim();
        ChanceValue = double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 999;
    }

    public string Name { get; }
    public string Chance { get; }
    public double ChanceValue { get; }
    public Bitmap? Image { get; }
}
