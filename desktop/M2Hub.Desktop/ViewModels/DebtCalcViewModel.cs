using System.Collections.ObjectModel;
using System.Globalization;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.ViewModels;

/// Schulden-Rechner: wie viele Laeufe fehlen noch, bis die Schuld bezahlt ist.
///
/// **Eine Schuld fuer alles.** Schuld und Abbezahltes gelten unabhaengig vom
/// Lauf und stehen in den Einstellungen (`SettingsData.Debt`); der
/// Laufwechsel aendert nur die Schaetzung, nicht den Fortschritt. Beides
/// traegt der Nutzer von Hand ein - was abbezahlt ist, weiss nur er, und es
/// kommt nicht nur aus Laeufen. Angegeben in **Won**, so wie man darueber
/// redet; gerechnet wird in kk (`Money`).
///
/// Die Truhen je Lauf kommen aus den eigenen Eintraegen im Run Tracker oder,
/// umschaltbar, von Hand - fuer Laeufe ohne Eintraege und fuer eigene
/// Annahmen. Dasselbe beim Preis: Spanne oder Durchschnitt aus der
/// Gesamtstatistik.
///
/// Der Rechenweg liegt in `Services/Calc/DebtCalc`; hier steht nur, was
/// eingestellt ist und wie es beschriftet wird.
public sealed class DebtCalcViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly StreamOverlay _stream;

    private RunChip _run;

    public DebtCalcViewModel(LocalStore store, StreamOverlay stream)
    {
        _store = store;
        _stream = stream;

        foreach (var run in RunCatalog.Runs) Runs.Add(new RunChip(run.Key, run.Name));
        _run = Runs.FirstOrDefault(c => c.Key == State.Run) ?? Runs[0];
        _run.IsActive = true;

        ChooseRunCommand = new RelayCommand(p => { if (p is RunChip chip) Run = chip; });
        UseAverageCommand = new RelayCommand(_ => UseAveragePrice = true);
        UseRangeCommand = new RelayCommand(_ => UseAveragePrice = false);
        UseStatsChestsCommand = new RelayCommand(_ => UseManualChests = false);
        UseManualChestsCommand = new RelayCommand(_ => UseManualChests = true);
    }

    private DebtData State => _store.Settings.Debt;

    private void Save()
    {
        _store.SaveSettings();

        // Die Schuld steht auch im Stream.
        _stream.Write();
        RaiseResult();
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
            State.Run = _run.Key;
            Save();
        }
    }

    public RelayCommand ChooseRunCommand { get; }

    private RunCatalog.Run Current => RunCatalog.Find(_run.Key) ?? RunCatalog.Runs[0];

    /* ---------- Eingaben ---------- */

    /// Die Schuld in Won - „2000 Won", wie man es sagt.
    public string DebtText
    {
        get => Text(State.Total);
        set { if (Read(value, out var won)) { State.Total = won; Save(); } Raise(nameof(DebtText)); }
    }

    /// Was davon schon abbezahlt ist, ebenfalls in Won. Von Hand, weil es
    /// nicht nur aus Laeufen kommt.
    public string FarmedText
    {
        get => Text(State.Farmed);
        set { if (Read(value, out var won)) { State.Farmed = won; Save(); } Raise(nameof(FarmedText)); }
    }

    /// Die Preisspanne je Truhe, in kk. Der Preis schwankt im Laufe des Tages;
    /// eine Spanne trifft es besser als eine Zahl.
    public string PriceLowText
    {
        get => Text(State.PriceLow);
        set { if (Read(value, out var kk)) { State.PriceLow = kk; Save(); } Raise(nameof(PriceLowText)); }
    }

    public string PriceHighText
    {
        get => Text(State.PriceHigh);
        set { if (Read(value, out var kk)) { State.PriceHigh = kk; Save(); } Raise(nameof(PriceHighText)); }
    }

    /// Truhen je Lauf von Hand - fuer Laeufe ohne Eintraege und fuer eigene
    /// Annahmen.
    public string ManualChestsText
    {
        get => Text(State.ManualChests);
        set { if (Read(value, out var n)) { State.ManualChests = n; Save(); } Raise(nameof(ManualChestsText)); }
    }

    /// Statt der Spanne der Durchschnitt aus der Gesamtstatistik des Laufs.
    public bool UseAveragePrice
    {
        get => State.UseAveragePrice;
        set
        {
            if (State.UseAveragePrice == value) return;

            State.UseAveragePrice = value;
            Raise(nameof(UseAveragePrice));
            Raise(nameof(UsePriceRange));
            Save();
        }
    }

    public bool UsePriceRange => !State.UseAveragePrice;

    /// Truhen je Lauf von Hand statt aus den eigenen Eintraegen.
    public bool UseManualChests
    {
        get => State.UseManualChests;
        set
        {
            if (State.UseManualChests == value) return;

            State.UseManualChests = value;
            Raise(nameof(UseManualChests));
            Raise(nameof(UseStatsChests));
            Save();
        }
    }

    public bool UseStatsChests => !State.UseManualChests;

    public RelayCommand UseAverageCommand { get; }
    public RelayCommand UseRangeCommand { get; }
    public RelayCommand UseStatsChestsCommand { get; }
    public RelayCommand UseManualChestsCommand { get; }

    /* ---------- Was aus den Eintraegen kommt ---------- */

    private IEnumerable<RunEntryDto> Mine() => _store.Runs.Entries.Where(e => e.Run == _run.Key);

    /// Truhen je Lauf aus allen Eintraegen dieses Laufs.
    private decimal StatsChestsPerRun
    {
        get
        {
            var list = Mine().ToList();
            return list.Count == 0 ? 0m : (decimal)list.Sum(e => e.Chests) / list.Count;
        }
    }

    /// Die Zahl, mit der gerechnet wird: von Hand oder aus den Eintraegen.
    private decimal ChestsPerRun => State.UseManualChests ? State.ManualChests : StatsChestsPerRun;

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

    /// Der Preis, mit dem gerechnet wird. Bei der Spanne die Mitte - fuer die
    /// Spanne der Laeufe stehen die Enden selbst.
    private decimal Price => State.UseAveragePrice
        ? AveragePrice
        : (State.PriceLow + State.PriceHigh) / 2m;

    public bool HasEntries => Mine().Any();

    /// Ohne Eintraege gibt es weder Truhenzahl noch Durchschnittspreis. Dann
    /// steht ein Hinweis da - und die Zahl von Hand hilft weiter.
    public bool NeedsManualChests => !HasEntries && !State.UseManualChests;

    /// Der Durchschnittspreis braucht Eintraege; ohne sie ist er leer.
    public bool CanUseAveragePrice => HasEntries;

    public string ChestsPerRunLabel =>
        ChestsPerRun <= 0m ? "—" : ChestsPerRun.ToString("0.##", CultureInfo.CurrentCulture);

    public string AveragePriceLabel => HasEntries ? Money.FormatYang((double)AveragePrice) : "—";

    /* ---------- Ergebnis ---------- */

    private decimal RestKk => DebtCalc.Rest(State.Total, State.Farmed) * Money.KkPerW;

    public string RestLabel => Money.FormatYang((double)RestKk);

    /// Wie weit die Schuld abbezahlt ist, in Prozent - fuer den Balken.
    public double Progress => State.Total <= 0m
        ? 0
        : (double)Math.Clamp(State.Farmed / State.Total * 100m, 0m, 100m);

    public string ProgressLabel => Progress.ToString("0.#", CultureInfo.CurrentCulture) + " %";

    /// „ungefaehr 540 bis 670 Laeufe" - oder eine Zahl, wenn der Durchschnitt
    /// genommen wird.
    public string RunsLabel
    {
        get
        {
            if (ChestsPerRun <= 0m) return "—";
            if (RestKk <= 0m) return Loc.T("calc.debt.done");

            if (State.UseAveragePrice)
            {
                var runs = DebtCalc.Runs(RestKk, DebtCalc.PerRun(ChestsPerRun, AveragePrice));
                return runs == 0 ? "—" : Loc.T("calc.debt.runs", Number(runs));
            }

            var (min, max) = DebtCalc.RunsBetween(
                RestKk, ChestsPerRun, State.PriceLow, State.PriceHigh);
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
            var perRun = DebtCalc.PerRun(ChestsPerRun, Price);
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
            if (ChestsPerRun <= 0m || RestKk <= 0m) return "—";

            var price = State.UseAveragePrice ? AveragePrice : Math.Max(State.PriceLow, State.PriceHigh);
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
        Raise(nameof(NeedsManualChests));
        Raise(nameof(CanUseAveragePrice));
        Raise(nameof(ChestsPerRunLabel));
        Raise(nameof(AveragePriceLabel));
        Raise(nameof(RestLabel));
        Raise(nameof(Progress));
        Raise(nameof(ProgressLabel));
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
