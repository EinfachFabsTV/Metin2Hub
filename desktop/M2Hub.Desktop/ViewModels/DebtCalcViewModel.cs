using System.Collections.ObjectModel;
using System.Globalization;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.ViewModels;

/// Schulden-Rechner: wie viele Laeufe fehlen noch, bis die Schuld bezahlt ist.
///
/// Schuld und Erfarmtes traegt man **von Hand** ein - was man abbezahlt hat,
/// weiss nur man selbst, und es kommt nicht nur aus Laeufen. Beides steht in
/// **Won**, so wie man darueber redet; gerechnet wird in kk (`Money`).
///
/// Die Truhen je Lauf kommen dagegen aus den eigenen Eintraegen im Run
/// Tracker - danach ist der Bereich ja da. Beim Preis hat man die Wahl: eine
/// Spanne von Hand („50 bis 62 kk") oder der Durchschnitt aus der
/// Gesamtstatistik des Laufs.
///
/// Der Rechenweg liegt in `Services/Calc/DebtCalc`; hier steht nur, was
/// eingestellt ist und wie es beschriftet wird.
public sealed class DebtCalcViewModel : ViewModelBase
{
    private readonly LocalStore _store;

    private RunChip _run;
    private decimal _debt = 2000m;
    private decimal _farmed;
    private decimal _priceLow = 50m;
    private decimal _priceHigh = 62m;
    private bool _useAverage;

    public DebtCalcViewModel(LocalStore store)
    {
        _store = store;

        foreach (var run in RunCatalog.Runs) Runs.Add(new RunChip(run.Key, run.Name));
        _run = Runs[0];
        _run.IsActive = true;

        ChooseRunCommand = new RelayCommand(p => { if (p is RunChip chip) Run = chip; });
        UseAverageCommand = new RelayCommand(_ => UseAverage = true);
        UseRangeCommand = new RelayCommand(_ => UseAverage = false);
    }

    /* ---------- Auswahl ---------- */

    public ObservableCollection<RunChip> Runs { get; } = new();

    public RunChip Run
    {
        get => _run;
        set
        {
            if (value is null || !Set(ref _run, value)) return;

            foreach (var chip in Runs) chip.IsActive = chip.Key == _run.Key;
            RaiseResult();
        }
    }

    public RelayCommand ChooseRunCommand { get; }

    private RunCatalog.Run Current => RunCatalog.Find(_run.Key) ?? RunCatalog.Runs[0];

    /* ---------- Eingaben ---------- */

    /// Die Schuld in Won - „2000 Won", wie man es sagt.
    public string DebtText
    {
        get => Text(_debt);
        set { if (Read(value, out var won)) { _debt = won; RaiseResult(); } Raise(nameof(DebtText)); }
    }

    /// Was davon schon abbezahlt ist, ebenfalls in Won. Von Hand, weil es
    /// nicht nur aus Laeufen kommt.
    public string FarmedText
    {
        get => Text(_farmed);
        set { if (Read(value, out var won)) { _farmed = won; RaiseResult(); } Raise(nameof(FarmedText)); }
    }

    /// Die Preisspanne je Truhe, in kk. Der Preis schwankt im Laufe des Tages;
    /// eine Spanne trifft es besser als eine Zahl.
    public string PriceLowText
    {
        get => Text(_priceLow);
        set { if (Read(value, out var kk)) { _priceLow = kk; RaiseResult(); } Raise(nameof(PriceLowText)); }
    }

    public string PriceHighText
    {
        get => Text(_priceHigh);
        set { if (Read(value, out var kk)) { _priceHigh = kk; RaiseResult(); } Raise(nameof(PriceHighText)); }
    }

    /// Statt der Spanne der Durchschnitt aus der Gesamtstatistik des Laufs.
    public bool UseAverage
    {
        get => _useAverage;
        set
        {
            if (!Set(ref _useAverage, value)) return;

            Raise(nameof(UseRange));
            RaiseResult();
        }
    }

    public bool UseRange => !_useAverage;

    public RelayCommand UseAverageCommand { get; }
    public RelayCommand UseRangeCommand { get; }

    /* ---------- Was aus den Eintraegen kommt ---------- */

    private IEnumerable<RunEntryDto> Mine() => _store.Runs.Entries.Where(e => e.Run == _run.Key);

    /// Truhen je Lauf, aus allen Eintraegen dieses Laufs. Ohne Eintraege gibt
    /// es keinen Erfahrungswert - dann sagt die Ansicht das, statt eine Zahl
    /// zu erfinden.
    private decimal ChestsPerRun
    {
        get
        {
            var list = Mine().ToList();
            return list.Count == 0 ? 0m : (decimal)list.Sum(e => e.Chests) / list.Count;
        }
    }

    /// Der Durchschnittspreis je Truhe ueber alles, nach Truhen gewichtet -
    /// derselbe Wert, den die Gesamtstatistik im Run Tracker zeigt.
    private decimal AveragePrice
    {
        get
        {
            var chests = Mine().Sum(e => e.Chests);
            if (chests == 0) return 0m;

            var fallback = _store.Runs.ChestPrice.TryGetValue(_run.Key, out var p)
                ? p
                : RunCatalog.DefaultChestPrice;

            return Mine().Sum(e => e.Chests * (e.Price > 0m ? e.Price : fallback)) / chests;
        }
    }

    public bool HasEntries => Mine().Any();

    public string ChestsPerRunLabel => HasEntries
        ? ChestsPerRun.ToString("0.##", CultureInfo.CurrentCulture)
        : "—";

    public string AveragePriceLabel => HasEntries ? Money.FormatYang((double)AveragePrice) : "—";

    /* ---------- Ergebnis ---------- */

    private decimal RestKk => DebtCalc.Rest(_debt, _farmed) * Money.KkPerW;

    public string RestLabel => Money.FormatYang((double)RestKk);

    /// „ungefaehr 540 bis 670 Laeufe" - oder eine Zahl, wenn der Durchschnitt
    /// genommen wird.
    public string RunsLabel
    {
        get
        {
            if (!HasEntries) return "—";
            if (RestKk <= 0m) return Loc.T("calc.debt.done");

            if (_useAverage)
            {
                var runs = DebtCalc.Runs(RestKk, DebtCalc.PerRun(ChestsPerRun, AveragePrice));
                return runs == 0 ? "—" : Loc.T("calc.debt.runs", Number(runs));
            }

            var (min, max) = DebtCalc.RunsBetween(RestKk, ChestsPerRun, _priceLow, _priceHigh);
            if (min == 0 || max == 0) return "—";

            return min == max
                ? Loc.T("calc.debt.runs", Number(min))
                : Loc.T("calc.debt.runsBetween", Number(min), Number(max));
        }
    }

    /// Was ein Lauf im Mittel einbringt - die Zahl, aus der sich alles ergibt.
    public string PerRunLabel
    {
        get
        {
            if (!HasEntries) return "—";

            var price = _useAverage ? AveragePrice : (_priceLow + _priceHigh) / 2m;
            var perRun = DebtCalc.PerRun(ChestsPerRun, price);
            return perRun <= 0m ? "—" : Money.FormatYang((double)perRun);
        }
    }

    /// Wie lange das dauert, wenn man die Abklingzeit abwartet. Bei der Hydra
    /// sind zwanzig Minuten zwischen zwei Laeufen; das macht aus einer Zahl
    /// Laeufe eine Zahl Tage.
    public string DurationLabel
    {
        get
        {
            if (!HasEntries || RestKk <= 0m) return "—";

            var price = _useAverage ? AveragePrice : Math.Max(_priceLow, _priceHigh);
            var runs = DebtCalc.Runs(RestKk, DebtCalc.PerRun(ChestsPerRun, price));
            if (runs == 0) return "—";

            var span = DebtCalc.Duration(runs, Cooldown);
            if (span == TimeSpan.Zero) return "—";

            return span.TotalHours < 24
                ? Loc.T("calc.debt.hours", Math.Round(span.TotalHours, 1))
                : Loc.T("calc.debt.days", Math.Round(span.TotalDays, 1));
        }
    }

    /// Die Abklingzeit des Laufs, wie sie im Run Tracker eingestellt ist.
    private int Cooldown =>
        _store.Runs.Cooldown.TryGetValue(_run.Key, out var m) ? m : Current.CooldownMinutes;

    public string NoEntriesHint => Loc.T("calc.debt.noEntries", Current.Name);

    /// Nach einem Sprachwechsel: die Namen der Laeufe stehen fest, die Texte
    /// darum herum nicht.
    public void RelabelAfterLanguageChange() => RaiseResult();

    /// Der Rechner liest aus `runs.json` - wer gerade Laeufe eingetragen hat,
    /// soll sie hier sofort sehen.
    public void Reload() => RaiseResult();

    private void RaiseResult()
    {
        Raise(nameof(HasEntries));
        Raise(nameof(ChestsPerRunLabel));
        Raise(nameof(AveragePriceLabel));
        Raise(nameof(RestLabel));
        Raise(nameof(RunsLabel));
        Raise(nameof(PerRunLabel));
        Raise(nameof(DurationLabel));
        Raise(nameof(NoEntriesHint));
    }

    /* ---------- Zahlen lesen und schreiben ---------- */

    private static string Text(decimal value) => value.ToString("0.##", CultureInfo.CurrentCulture);

    /// Wie beim Truhenpreis: Punkt wie Komma, und Unlesbares bleibt stehen.
    private static bool Read(string? text, out decimal value) =>
        decimal.TryParse((text ?? "").Trim().Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0m;

    private static string Number(long value) => value.ToString("N0", CultureInfo.CurrentCulture);
}
