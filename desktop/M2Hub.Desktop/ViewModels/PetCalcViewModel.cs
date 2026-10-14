using System.Collections.ObjectModel;
using System.Globalization;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.ViewModels;

/// Pet-Rechner mit zwei Bereichen: Fuetterbedarf (wie viel Futter und was es
/// kostet) und Petwerte (Statusprognose nach Typ, Level und Alter).
///
/// Uebernommen aus der Website ('Veraltet/app/pages/rechner/pet.vue'); der
/// Rechenweg liegt in Services/Calc/PetCalc. Hier steht nur, was eingestellt
/// ist und wie das Ergebnis beschriftet wird.
public sealed class PetCalcViewModel : ViewModelBase
{
    private string _tab = "futter";

    /* ---------- Fuetterbedarf ---------- */
    private int _from = 50;
    private int _to = 105;
    private string _priceWeaponText = "";
    private string _priceSnackText = "";
    private string _priceDeliciousText = "";

    /* ---------- Petwerte ---------- */
    private bool _champion;
    private int _petType = 1;
    private int _statFrom = 1;
    private int _statTo = 105;
    private int _age;
    private string _currentAText = "1";
    private string _currentBText = "1";
    private string _currentCText = "1";

    public PetCalcViewModel()
    {
        ChooseTabCommand = new RelayCommand(p => Tab = p as string ?? _tab);
        SetChampionCommand = new RelayCommand(p => Champion = p as string == "champion");
        RecalcFeed();
        RecalcStats();
    }

    /* ======================= Bereichswahl ======================= */

    public string Tab
    {
        get => _tab;
        set
        {
            if (!Set(ref _tab, value)) return;
            Raise(nameof(IsFeed));
            Raise(nameof(IsStats));
        }
    }

    public bool IsFeed => _tab == "futter";
    public bool IsStats => _tab == "werte";
    public RelayCommand ChooseTabCommand { get; }

    /* ======================= Fuetterbedarf ======================= */

    public int MinSlider => PetCalc.MinLevel;                       // 50
    public int MaxSlider => PetCalc.Weapons.Keys.Max() + 1;         // bis CL30

    public int From
    {
        get => _from;
        set
        {
            if (!Set(ref _from, Math.Clamp(value, MinSlider, MaxSlider))) return;
            if (_to < _from) To = _from;
            RecalcFeed();
        }
    }

    public int To
    {
        get => _to;
        set
        {
            if (!Set(ref _to, Math.Clamp(value, MinSlider, MaxSlider))) return;
            if (_to < _from) From = _to;
            RecalcFeed();
        }
    }

    public string FromLabel => LevelLabel(_from);
    public string ToLabel => LevelLabel(_to);

    public string PriceWeaponText { get => _priceWeaponText; set { if (Set(ref _priceWeaponText, value)) RecalcFeed(); } }
    public string PriceSnackText { get => _priceSnackText; set { if (Set(ref _priceSnackText, value)) RecalcFeed(); } }
    public string PriceDeliciousText { get => _priceDeliciousText; set { if (Set(ref _priceDeliciousText, value)) RecalcFeed(); } }

    private PetCalc.FeedResult Feed =>
        PetCalc.Feed(_from, _to, ParsePrice(_priceWeaponText), ParsePrice(_priceSnackText), ParsePrice(_priceDeliciousText));

    public string FeedSubtitle => Loc.T("calc.pet.feed.resultSubtitle", LevelLabel(_from), LevelLabel(_to));

    // Die beiden Kernzahlen: benoetigte Item-EXP und normale Pet-EXP.
    public string ItemExpValue => Number(ExpNeeded().Item);
    public string NormalExpValue => Number(ExpNeeded().Total);

    private (long Item, long Total, bool Cl) ExpNeeded()
    {
        var a = Math.Min(_from, _to);
        var b = Math.Max(_from, _to);
        const int max = PetCalc.MaxLevel; // 105

        if (a > max)
        {
            var ca = a - max;
            var cb = b - max;
            return (PetCalc.ClItemExpBetween(ca, cb), PetCalc.ClExpBetween(ca, cb), true);
        }

        var capped = Math.Min(b, max);
        var clPart = b > max ? b - max : 0;
        return (
            PetCalc.ItemExpBetween(a, capped) + (clPart > 0 ? PetCalc.ClItemExpBetween(0, clPart) : 0),
            PetCalc.ExpBetween(a, capped) + (clPart > 0 ? PetCalc.ClExpBetween(0, clPart) : 0),
            clPart > 0);
    }

    // Weg 1: Item-EXP (Waffe/Leckerli+, austauschbar). Weg 2: normale Pet-EXP (Koestliches Leckerli).
    public ObservableCollection<PetFoodRow> ItemRows { get; } = new();
    public ObservableCollection<PetFoodRow> ExpRows { get; } = new();

    public bool HasPrices => Feed.Options.Any(o => o.Cost is not null);
    public bool HasBest => Feed.Best is not null && HasPrices;
    public string BestLabel => Feed.Best is { } k ? FoodName(k) : "–";

    // Ab welchem Level Waffe +7 bzw. Leckerli+ die bessere Wahl ist - nach
    // Stueckzahl immer, nach Preis sobald beide Preise stehen.
    public string AmountCrossoverText => CrossoverText(
        PetCalc.AmountCrossover(_from, _to), "calc.pet.feed.crossAmountAt", "calc.pet.feed.crossAmountNever");

    public bool ShowPriceCrossover =>
        PetCalc.PriceCrossover(_from, _to, ParsePrice(_priceWeaponText), ParsePrice(_priceSnackText)) is not null;

    public string PriceCrossoverText
    {
        get
        {
            var c = PetCalc.PriceCrossover(_from, _to, ParsePrice(_priceWeaponText), ParsePrice(_priceSnackText));
            return c is null ? "" : CrossoverText(c, "calc.pet.feed.crossPriceAt", "calc.pet.feed.crossPriceNever");
        }
    }

    private string CrossoverText(PetCalc.Crossover c, string atKey, string neverKey)
    {
        var first = FoodName(c.First);
        if (c.SwitchLevel is not { } level)
            return Loc.T(neverKey, first);
        var other = FoodName(c.First == PetCalc.FoodKind.Weapon ? PetCalc.FoodKind.Snack : PetCalc.FoodKind.Weapon);
        return Loc.T(atKey, first, LevelLabel(level), other);
    }

    public bool ShowClNote => ExpNeeded().Cl;
    public bool ShowMissing => Feed.MissingDelicious.Count > 0;
    public string MissingText =>
        Loc.T("calc.pet.feed.missingDelicious", string.Join(", ", Feed.MissingDelicious.Select(LevelLabel)));

    private void RecalcFeed()
    {
        var feed = Feed;
        var best = feed.Best;

        ItemRows.Clear();
        ExpRows.Clear();
        foreach (var o in feed.Options)
        {
            var row = new PetFoodRow(
                FoodName(o.Kind),
                $"{Decimal1(o.Amount)} {UnitName(o.Kind)}",
                o.Cost is { } c ? Money.FormatYang(c) : "—",
                o.Track == PetCalc.PetTrack.Item && best == o.Kind,
                o.Kind == PetCalc.FoodKind.Delicious && feed.MissingDelicious.Count > 0);
            if (o.Track == PetCalc.PetTrack.Item) ItemRows.Add(row);
            else ExpRows.Add(row);
        }

        Raise(nameof(FromLabel));
        Raise(nameof(ToLabel));
        Raise(nameof(FeedSubtitle));
        Raise(nameof(ItemExpValue));
        Raise(nameof(NormalExpValue));
        Raise(nameof(HasPrices));
        Raise(nameof(HasBest));
        Raise(nameof(BestLabel));
        Raise(nameof(ShowClNote));
        Raise(nameof(ShowMissing));
        Raise(nameof(MissingText));
        Raise(nameof(AmountCrossoverText));
        Raise(nameof(ShowPriceCrossover));
        Raise(nameof(PriceCrossoverText));
    }

    /* ======================= Petwerte ======================= */

    public bool Champion
    {
        get => _champion;
        set
        {
            if (!Set(ref _champion, value)) return;
            Raise(nameof(NotChampion));
            _statFrom = 1;
            _statTo = StatMax;
            Raise(nameof(StatFrom));
            Raise(nameof(StatTo));
            Raise(nameof(StatMax));
            Raise(nameof(StatFromLabel));
            Raise(nameof(StatToLabel));
            Raise(nameof(CurrentALabel));
            Raise(nameof(CurrentBLabel));
            Raise(nameof(CurrentCLabel));
            Raise(nameof(StartBonusValue));
            RecalcStats();
        }
    }

    public bool NotChampion => !_champion;
    public RelayCommand SetChampionCommand { get; }

    // Championlevel gehen nur bis 30, normale Level bis 105.
    public int StatMax => _champion ? 30 : PetCalc.MaxLevel;

    public int PetType
    {
        get => _petType;
        set { if (Set(ref _petType, Math.Clamp(value, 1, 8))) { Raise(nameof(PetTypeLabel)); RecalcStats(); } }
    }

    public string PetTypeLabel => Loc.T("calc.pet.stats.typeBadge", _petType);

    public int StatFrom
    {
        get => _statFrom;
        set
        {
            if (!Set(ref _statFrom, Math.Clamp(value, 1, StatMax))) return;
            if (_statTo < _statFrom) StatTo = _statFrom;
            Raise(nameof(StatFromLabel));
            RecalcStats();
        }
    }

    public int StatTo
    {
        get => _statTo;
        set
        {
            if (!Set(ref _statTo, Math.Clamp(value, 1, StatMax))) return;
            if (_statTo < _statFrom) StatFrom = _statTo;
            Raise(nameof(StatToLabel));
            RecalcStats();
        }
    }

    public string StatFromLabel => _champion ? $"CL{_statFrom}" : _statFrom.ToString();
    public string StatToLabel => _champion ? $"CL{_statTo}" : _statTo.ToString();

    public int Age
    {
        get => _age;
        set { if (Set(ref _age, Math.Clamp(value, 0, PetCalc.AgeMaxDays))) { Raise(nameof(AgeLabel)); RecalcStats(); } }
    }

    public int AgeMax => PetCalc.AgeMaxDays;
    public string AgeLabel => $"{_age} {Loc.T(_age == 1 ? "calc.pet.day.one" : "calc.pet.day.many")}";
    public string AgeNote => Loc.T("calc.pet.stats.ageNote", Percent(PetCalc.AgeBonusMax), PetCalc.AgeMaxDays);

    public string CurrentAText { get => _currentAText; set { if (Set(ref _currentAText, value)) RecalcStats(); } }
    public string CurrentBText { get => _currentBText; set { if (Set(ref _currentBText, value)) RecalcStats(); } }
    public string CurrentCText { get => _currentCText; set { if (Set(ref _currentCText, value)) RecalcStats(); } }

    public string CurrentALabel => Loc.T(_champion ? "calc.pet.stats.cur.res" : "calc.pet.stats.cur.hp");
    public string CurrentBLabel => Loc.T(_champion ? "calc.pet.stats.cur.block" : "calc.pet.stats.cur.def");
    public string CurrentCLabel => Loc.T(_champion ? "calc.pet.stats.cur.precision" : "calc.pet.stats.cur.mp");

    public ObservableCollection<PetStatRow> StatRows { get; } = new();

    public string ForecastSub => Loc.T("calc.pet.stats.forecastSub",
        StatFromLabel, StatToLabel, _age, Loc.T(_age == 1 ? "calc.pet.day.one" : "calc.pet.day.many"));

    public string AgeBonusValue => "+" + Percent(PetCalc.AgeBonus(_age));

    public string StartBonusValue => _champion
        ? $"{Percent(PetCalc.ClStartBonus.Min)} – {Percent(PetCalc.ClStartBonus.Max)}"
        : $"{Percent(PetCalc.StartBonusHp.Min)} – {Percent(PetCalc.StartBonusHp.Max)}";

    private void RecalcStats()
    {
        var r = PetCalc.StatsForecast(_petType, _statFrom, _statTo, _age,
            ParseStat(_currentAText), ParseStat(_currentBText), ParseStat(_currentCText), _champion);

        StatRows.Clear();
        StatRows.Add(Row(CurrentALabel, r.A));
        StatRows.Add(Row(CurrentBLabel, r.B));
        StatRows.Add(Row(CurrentCLabel, r.C));

        Raise(nameof(ForecastSub));
        Raise(nameof(AgeBonusValue));
        Raise(nameof(StartBonusValue));

        PetStatRow Row(string label, PetCalc.StatForecast f) =>
            new(label, f.Upgrades.ToString(), Percent(f.Min), Percent(f.Avg), Percent(f.Max));
    }

    /* ======================= Beschriftung & Format ======================= */

    public void RelabelAfterLanguageChange()
    {
        RecalcFeed();
        Raise(nameof(PetTypeLabel));
        Raise(nameof(AgeLabel));
        Raise(nameof(AgeNote));
        Raise(nameof(CurrentALabel));
        Raise(nameof(CurrentBLabel));
        Raise(nameof(CurrentCLabel));
        RecalcStats();
    }

    private static string LevelLabel(int v) =>
        v > PetCalc.MaxLevel ? $"CL{v - PetCalc.ClOffset}" : v.ToString();

    private string FoodName(PetCalc.FoodKind kind) => Loc.T(kind switch
    {
        PetCalc.FoodKind.Weapon => "calc.pet.opt.weapon",
        PetCalc.FoodKind.Snack => "calc.pet.opt.snack",
        _ => "calc.pet.opt.delicious",
    });

    private string UnitName(PetCalc.FoodKind kind) =>
        Loc.T(kind == PetCalc.FoodKind.Delicious ? "calc.pet.unit.stack" : "calc.pet.unit.piece");

    private static double? ParsePrice(string? text) =>
        double.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0
            ? v : null;

    private static double ParseStat(string? text) =>
        double.TryParse((text ?? "").Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string Number(long value) => value.ToString("N0");
    private static string Decimal1(double value) => value.ToString("0.#", CultureInfo.CurrentCulture);

    private static string Percent(double value) =>
        value.ToString("0.0##", CultureInfo.CurrentCulture) + " %";
}

/// Eine Zeile der Futter-Tabellen.
public sealed record PetFoodRow(string Food, string Amount, string Cost, bool Best, bool Incomplete);

/// Eine Zeile der Werteprognose.
public sealed record PetStatRow(string Stat, string Upgrades, string Min, string Avg, string Max);
