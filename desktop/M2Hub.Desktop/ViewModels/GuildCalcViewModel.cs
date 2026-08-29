using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.ViewModels;

/// Gilden-Rechner: wie viele Spenden, Medaillen, Tage und Drachenmuenzen ein
/// Gildenaufstieg kostet.
///
/// Uebernommen aus der Website ('Veraltet/app/pages/rechner/gilde.vue'). Der
/// Rechenweg liegt in Services/Calc/GuildCalc; hier steht nur, was eingestellt
/// ist und wie das Ergebnis beschriftet wird. Gerechnet wird bei jeder
/// Aenderung neu - die Zahlen sind klein genug, dass es nichts zu sparen gibt.
public sealed class GuildCalcViewModel : ViewModelBase
{
    private int _currentLevel = GuildCalc.MinLevel;
    private int _targetLevel = 30;
    private int _chars = 1;
    private string _donation = GuildCalc.TicketKey;

    public GuildCalcViewModel()
    {
        ChooseDonationCommand = new RelayCommand(p => ChooseDonation(p as DonationOption));
        RebuildDonationTypes();
    }

    /* ---------- Grenzen fuer die Regler ---------- */

    public int MinLevel => GuildCalc.MinLevel;
    public int MaxLevel => GuildCalc.MaxLevel;
    public int MaxMembers => GuildCalc.MaxMembers;

    /// Steht unter dem Regler: drei Spenden je Charakter und Tag, hoechstens
    /// 90 Mitglieder.
    public string PerDayNote =>
        Loc.T("calc.guild.perDayNote", GuildCalc.DonationsPerDay, GuildCalc.MaxMembers);

    /* ---------- Eingaben ---------- */

    /// Das Ziel bleibt nie unter dem aktuellen Level - sonst stuende dort ein
    /// Aufstieg, den es nicht gibt.
    public int CurrentLevel
    {
        get => _currentLevel;
        set
        {
            if (!Set(ref _currentLevel, Math.Clamp(value, MinLevel, MaxLevel))) return;
            if (_targetLevel < _currentLevel) TargetLevel = _currentLevel;
            Recalculate();
        }
    }

    public int TargetLevel
    {
        get => _targetLevel;
        set
        {
            if (!Set(ref _targetLevel, Math.Clamp(value, MinLevel, MaxLevel))) return;
            if (_targetLevel < _currentLevel) CurrentLevel = _targetLevel;
            Recalculate();
        }
    }

    /// Wie viele Charaktere spenden. Mehr als die Mitgliedergrenze der Gilde
    /// geht nicht.
    public int Chars
    {
        get => _chars;
        set { if (Set(ref _chars, Math.Clamp(value, 1, MaxMembers))) Recalculate(); }
    }

    /// Spendenarten als Knopfreihe - jede traegt ihre EXP im Text. Die
    /// gewaehlte ist hervorgehoben, wie in der Vorlage.
    public ObservableCollection<DonationOption> DonationTypes { get; } = new();

    public RelayCommand ChooseDonationCommand { get; }

    public DonationOption DonationType =>
        DonationTypes.FirstOrDefault(o => o.Key == _donation) ?? DonationTypes[0];

    private void ChooseDonation(DonationOption? option)
    {
        if (option is null || !Set(ref _donation, option.Key)) return;
        foreach (var o in DonationTypes) o.IsActive = o.Key == _donation;
        Raise(nameof(DonationType));
        Recalculate();
    }

    /* ---------- Ergebnis ---------- */

    private GuildCalc.GuildResult Result =>
        GuildCalc.Donations(_currentLevel, _targetLevel, _chars, _donation);

    public string CurrentLevelLabel => Loc.T("calc.guild.levelBadge", _currentLevel);
    public string TargetLevelLabel => Loc.T("calc.guild.levelBadge", _targetLevel);
    public string CharsLabel => $"{_chars} {Loc.T(_chars == 1 ? "calc.char.one" : "calc.char.many")}";
    public string ResultSubtitle => Loc.T("calc.guild.resultSubtitle", _currentLevel, _targetLevel);

    public string Donations => Number(Result.Donations);
    public string DonationsCaption => Loc.T("calc.guild.donationsFor", DonationType.Label);

    /// Nur der mittlere Schein ist kaufbar; ohne ihn gibt es keine Muenzzahl.
    public bool Buyable => Result.Buyable;
    public bool NotBuyable => !Result.Buyable;

    public string DragonCoinsPerAccount => Number(Result.DragonCoinsPerAccount);
    public string TotalExp => Number(Result.TotalExp);
    public string PerChar => Number(Result.PerChar);
    public string PerAccount => Number(Result.PerAccount);
    public string Medals => Number(Result.Medals);
    public string DragonCoinsPerChar => Number(Result.DragonCoinsPerChar);
    public string DragonCoins => Number(Result.DragonCoins);
    public string Days => Loc.T("calc.guild.daysValue", Number(Result.Days));
    public string DaysCaption => Loc.T("calc.guild.daysRow", CharsLabel);

    public string PerCharCaption =>
        Loc.T(Result.Buyable ? "calc.guild.ticketsPerChar" : "calc.guild.donationsPerChar");

    /// „18 Accounts à 5 Charaktere · 740 Scheine je Account"
    public string AccountsNote
    {
        get
        {
            var r = Result;
            var accounts = $"{r.Accounts} {Loc.T(r.Accounts == 1 ? "calc.account.one" : "calc.account.many")}";
            return Loc.T("calc.guild.accountsNote", accounts, GuildCalc.CharsPerAccount, Number(r.PerAccount));
        }
    }

    /// Wie weit das aktuelle Level auf dem Weg zum Ziel steht - fuer den
    /// Balken unter dem Ergebnis.
    public double Progress
    {
        get
        {
            var span = _targetLevel - GuildCalc.MinLevel;
            if (span <= 0) return 100;
            return Math.Round((_currentLevel - GuildCalc.MinLevel) * 100.0 / span);
        }
    }

    public string ProgressLabel => Loc.T("calc.guild.progress", _targetLevel);
    public string ProgressPercent => $"{Progress:0}%";

    /* ---------- Beschriftungen ---------- */

    /// Nach einem Sprachwechsel: die Knopfreihe und alle abgeleiteten Texte
    /// neu setzen. Die Auswahl selbst bleibt stehen.
    public void RelabelAfterLanguageChange()
    {
        RebuildDonationTypes();
        Recalculate();
    }

    private void RebuildDonationTypes()
    {
        var chosen = _donation;
        DonationTypes.Clear();
        foreach (var type in GuildCalc.DonationTypes)
        {
            var label = $"{Loc.T($"calc.guild.donation.{type.Key}")} ({Number(type.Exp)} {Loc.T("calc.guild.expSuffix")})";
            DonationTypes.Add(new DonationOption(type.Key, label) { IsActive = type.Key == chosen });
        }
        _donation = chosen;
        Raise(nameof(DonationType));
    }

    /// Alles am Ergebnis haengt an denselben vier Eingaben - deshalb in einem
    /// Aufwasch statt je Eigenschaft.
    private void Recalculate()
    {
        Raise(nameof(CurrentLevelLabel));
        Raise(nameof(TargetLevelLabel));
        Raise(nameof(CharsLabel));
        Raise(nameof(ResultSubtitle));
        Raise(nameof(Donations));
        Raise(nameof(DonationsCaption));
        Raise(nameof(Buyable));
        Raise(nameof(NotBuyable));
        Raise(nameof(DragonCoinsPerAccount));
        Raise(nameof(TotalExp));
        Raise(nameof(PerChar));
        Raise(nameof(PerCharCaption));
        Raise(nameof(PerAccount));
        Raise(nameof(Medals));
        Raise(nameof(DragonCoinsPerChar));
        Raise(nameof(DragonCoins));
        Raise(nameof(Days));
        Raise(nameof(DaysCaption));
        Raise(nameof(AccountsNote));
        Raise(nameof(Progress));
        Raise(nameof(ProgressLabel));
        Raise(nameof(ProgressPercent));
        Raise(nameof(PerDayNote));
    }

    /// Tausenderpunkte nach der eingestellten Sprache.
    private static string Number(long value) => value.ToString("N0");
}

/// Eine Spendenart in der Knopfreihe. Traegt neben der Beschriftung, ob sie
/// gerade gewaehlt ist - daran haengt die Hervorhebung.
public sealed class DonationOption(string key, string label) : ViewModelBase
{
    private bool _isActive;

    public string Key { get; } = key;
    public string Label { get; } = label;

    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
}
