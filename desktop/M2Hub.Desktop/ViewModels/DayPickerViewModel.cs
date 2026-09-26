using System.Collections.ObjectModel;
using System.Globalization;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Der Tageswaehler des Run Trackers.
///
/// Avalonia bringt ein `Calendar` mit, das aber sein eigenes Aussehen aus dem
/// Fluent-Thema zieht - heller Rahmen, eigene Schrift, eckige Auswahl. Neben
/// den Karten der App sah es aus wie ein Fremdkoerper. Deshalb dieselben
/// Bausteine wie im Eventkalender: `Border.cell` aus `Theme.axaml`, damit die
/// Farben aus der einen Quelle kommen.
///
/// Das Raster ist fest sechs Wochen hoch, sonst huepft die Karte je nach Monat.
public sealed class DayPickerViewModel : ViewModelBase
{
    private const int Weeks = 6;

    private readonly Action<DateTime> _picked;
    private DateTime _selected;
    private DateTime _month;

    public DayPickerViewModel(DateTime selected, Action<DateTime> picked)
    {
        _picked = picked;
        _selected = selected.Date;
        _month = new DateTime(_selected.Year, _selected.Month, 1);

        PrevCommand = new RelayCommand(_ => Shift(-1));
        NextCommand = new RelayCommand(_ => Shift(+1));
        PickCommand = new RelayCommand(p =>
        {
            if (p is DayCellViewModel cell) _picked(cell.Date);
        });

        Build();
    }

    public ObservableCollection<string> Heads { get; } = [];
    public ObservableCollection<DayCellViewModel> Cells { get; } = [];

    public RelayCommand PrevCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand PickCommand { get; }

    /// „September 2026" in der eingestellten Sprache.
    public string MonthLabel => Capitalize(_month.ToString("MMMM yyyy", Culture()));

    /// Von aussen gesetzt, wenn der Tag anderswo wechselt („Heute").
    public void Select(DateTime day)
    {
        day = day.Date;
        if (_selected == day && _month.Month == day.Month && _month.Year == day.Year) return;

        _selected = day;
        _month = new DateTime(day.Year, day.Month, 1);
        Build();
    }

    /// Nach einem Sprachwechsel: Monatsname und Wochentage neu schreiben.
    public void Refresh() => Build();

    private void Shift(int months)
    {
        _month = _month.AddMonths(months);
        Build();
    }

    private void Build()
    {
        var culture = Culture();
        var today = DateTime.Today;

        Heads.Clear();
        // Die Woche beginnt beim ersten Wochentag der Sprache - im Deutschen
        // Montag, anderswo kann es Sonntag sein.
        var first = (int)culture.DateTimeFormat.FirstDayOfWeek;
        for (var i = 0; i < 7; i++)
        {
            var day = (DayOfWeek)((first + i) % 7);
            Heads.Add(culture.DateTimeFormat.GetShortestDayName(day));
        }

        // Rueckwaerts bis zum ersten Wochentag vor dem Monatsanfang.
        var lead = ((int)_month.DayOfWeek - first + 7) % 7;
        var start = _month.AddDays(-lead);

        Cells.Clear();
        for (var i = 0; i < Weeks * 7; i++)
        {
            var date = start.AddDays(i);
            Cells.Add(new DayCellViewModel(
                date,
                date.Day.ToString(CultureInfo.InvariantCulture),
                date.Month == _month.Month,
                date == _selected,
                date == today));
        }

        Raise(nameof(MonthLabel));
    }

    private static CultureInfo Culture()
    {
        try { return CultureInfo.GetCultureInfo(Loc.I.Language); }
        catch (CultureNotFoundException) { return CultureInfo.CurrentCulture; }
    }

    /// Manche Sprachen schreiben den Monat klein; in der Ueberschrift steht er
    /// gross.
    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
}

/// Ein Tag im Raster. Rein lesend - angeklickt wird ueber das PickCommand.
public sealed class DayCellViewModel(
    DateTime date, string text, bool inMonth, bool isSelected, bool isToday)
{
    public DateTime Date { get; } = date;
    public string Text { get; } = text;

    /// Tage aus dem Vor- oder Folgemonat stehen blass da, bleiben aber
    /// waehlbar - so kommt man ohne Blaettern ueber den Monatswechsel.
    public bool InMonth { get; } = inMonth;
    public bool IsSelected { get; } = isSelected;
    public bool IsToday { get; } = isToday;
}
