using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Forum;

namespace M2Hub.Desktop.ViewModels;

/// Die Startseite: was heute ansteht, in Abschnitten untereinander.
///
/// Sie rechnet nichts selbst aus und schreibt nichts selbst. Die Zahlen kommen
/// aus der Ablage, das Eintragen von Medaillen geht ueber das Accounts-Modell -
/// sonst gaebe es zwei Stellen, an denen Medaillen gutgeschrieben werden.
///
/// Welche Abschnitte erscheinen, steht in den Einstellungen, je Betriebsart
/// eine eigene Reihenfolge (DashboardSections).
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly AccountsViewModel _accounts;

    public DashboardViewModel(
        LocalStore store,
        AccountsViewModel accounts,
        ObservableCollection<ActiveBadgeViewModel> activeNow,
        Action<string> go)
    {
        _store = store;
        _accounts = accounts;
        ActiveNow = activeNow;

        GoCommand = new RelayCommand(p => { if (p is string key) go(key); });
        OverviewCommand = new RelayCommand(_ => Mode = DashboardSections.Overview);
        WorkCommand = new RelayCommand(_ => Mode = DashboardSections.Work);
    }

    /* ---------- Betriebsart ---------- */

    /// „Uebersicht" oder „Arbeitsflaeche". Der Umschalter sitzt oben auf der
    /// Seite, damit er ohne Umweg ueber die Einstellungen erreichbar ist.
    public string Mode
    {
        get => _store.Settings.DashboardMode == DashboardSections.Work
            ? DashboardSections.Work
            : DashboardSections.Overview;
        set
        {
            if (Mode == value) return;
            _store.Settings.DashboardMode = value;
            _store.SaveSettings();
            Raise(nameof(Mode));
            Raise(nameof(IsOverview));
            Raise(nameof(IsWork));
            Reload();
        }
    }

    public bool IsOverview => Mode == DashboardSections.Overview;
    public bool IsWork => Mode == DashboardSections.Work;

    public RelayCommand OverviewCommand { get; }
    public RelayCommand WorkCommand { get; }

    /// Fuehrt in einen Bereich - die Verweise rechts oben in den Abschnitten.
    public RelayCommand GoCommand { get; }

    /* ---------- Abschnitte ---------- */

    /// Die gewaehlten Abschnitte der aktuellen Betriebsart, in ihrer
    /// Reihenfolge. Leer gespeichert heisst: die Vorgabe.
    public List<string> Chosen
    {
        get
        {
            var saved = Mode == DashboardSections.Work
                ? _store.Settings.DashboardWork
                : _store.Settings.DashboardOverview;

            if (saved.Count == 0) return [.. DashboardSections.Default(Mode)];

            // Unbekanntes aus einer aelteren Datei faellt still weg.
            return saved.Where(k => DashboardSections.All.Contains(k)).ToList();
        }
    }

    private bool Shows(string key) => Chosen.Contains(key);

    public bool ShowStats => Shows(DashboardSections.Stats);
    public bool ShowActive => Shows(DashboardSections.Active) && ActiveNow.Count > 0;
    public bool ShowCalendar => Shows(DashboardSections.Calendar) && HasCalendar;
    public bool ShowItemshop => Shows(DashboardSections.Itemshop) && Ending.Count > 0;
    public bool ShowMedals => Shows(DashboardSections.Medals) && Donors.Count > 0;
    public bool ShowBio => Shows(DashboardSections.Bio) && BioOpen.Count > 0;

    /// Ist nichts gewaehlt oder liegt zu allem nichts vor, sagt die Seite das -
    /// statt leer dazustehen.
    public bool Empty => !ShowStats && !ShowActive && !ShowCalendar
                         && !ShowItemshop && !ShowMedals && !ShowBio;

    /* ---------- Kennzahlen ---------- */

    public string TotalMedals => _accounts.TotalMedals.ToString("N0");
    public string AverageMedals => _accounts.AverageMedals.ToString("N0");
    public string TotalAccounts => _accounts.TotalAccounts.ToString("N0");
    public string TotalCharacters => _accounts.TotalCharacters.ToString("N0");
    public string TotalDragonCoins => _accounts.TotalDragonCoins.ToString("N0");
    public string BioProgress => _accounts.BioProgress;

    /* ---------- Jetzt aktiv ---------- */

    public ObservableCollection<ActiveBadgeViewModel> ActiveNow { get; }

    /* ---------- Heute im Kalender ---------- */

    public string CalendarServer { get; private set; } = "";
    public string CalendarNow { get; private set; } = "";
    public string CalendarLine { get; private set; } = "";
    public Bitmap? CalendarIcon { get; private set; }
    public bool HasCalendarIcon => CalendarIcon is not null;
    public bool HasCalendar => CalendarNow.Length > 0;

    /* ---------- Itemshop ---------- */

    public ObservableCollection<EndingItemViewModel> Ending { get; } = new();

    /* ---------- Arbeitsflaeche ---------- */

    /// Die Spenden-Chars mit ihrer Schnellwahl - auf sie wird taeglich
    /// gutgeschrieben, deshalb stehen genau sie hier.
    public ObservableCollection<CharacterItemViewModel> Donors { get; } = new();

    /// Accounts, deren Orkzahn-Bio noch offen ist.
    public ObservableCollection<AccountItemViewModel> BioOpen { get; } = new();

    /* ---------- Aufbauen ---------- */

    public void Reload()
    {
        BuildCalendar();
        BuildEnding();
        BuildWork();

        Raise(nameof(TotalMedals));
        Raise(nameof(AverageMedals));
        Raise(nameof(TotalAccounts));
        Raise(nameof(TotalCharacters));
        Raise(nameof(TotalDragonCoins));
        Raise(nameof(BioProgress));

        Raise(nameof(CalendarServer));
        Raise(nameof(CalendarNow));
        Raise(nameof(CalendarLine));
        Raise(nameof(CalendarIcon));
        Raise(nameof(HasCalendarIcon));

        RaiseSections();
    }

    /// Nach einer Aenderung in den Einstellungen: nur die Sichtbarkeiten neu
    /// melden, die Daten stehen schon.
    public void RaiseSections()
    {
        Raise(nameof(ShowStats));
        Raise(nameof(ShowActive));
        Raise(nameof(ShowCalendar));
        Raise(nameof(ShowItemshop));
        Raise(nameof(ShowMedals));
        Raise(nameof(ShowBio));
        Raise(nameof(Empty));
    }

    /// Der Server aus den Einstellungen; ist keiner gewaehlt, der erste, der
    /// gerade etwas laufen hat.
    private void BuildCalendar()
    {
        CalendarServer = "";
        CalendarNow = "";
        CalendarLine = "";
        CalendarIcon = null;

        var wanted = _store.Settings.HeaderServer;
        var now = Html.BerlinNow();

        foreach (var cal in _store.Cache.Servers)
        {
            if (wanted.Length > 0 && cal.Key != wanted) continue;
            if (ServerCatalog.IsHidden(_store, cal.Key)) continue;

            var current = EventCalendar.CurrentFor(cal, now);
            if (current is null) continue;

            CalendarServer = cal.Label;
            CalendarNow = Glossary.Term(current.Text);
            CalendarLine = Loc.T("events.now", current.From, current.To);
            CalendarIcon = EventIcons.Find(current.Text);
            return;
        }
    }

    /// Was im Itemshop als naechstes endet - hoechstens vier Zeilen, sonst
    /// waere es die Itemshop-Seite.
    private void BuildEnding()
    {
        Ending.Clear();

        var now = DateTime.Now;
        var soon = _store.Cache.Itemshop
            .Where(e => e.EndsAt is { } end && end.ToLocalTime() >= now)
            .OrderBy(e => e.EndsAt)
            .Take(4);

        foreach (var e in soon) Ending.Add(new EndingItemViewModel(e));
    }

    private void BuildWork()
    {
        Donors.Clear();
        BioOpen.Clear();

        foreach (var account in _accounts.AllAccounts)
        {
            foreach (var c in account.Characters)
                if (c.IsDonate) Donors.Add(c);

            if (!account.BioDone) BioOpen.Add(account);
        }
    }
}

/// Eine Zeile im Abschnitt „Itemshop": was es ist und wann es endet.
public sealed class EndingItemViewModel
{
    public EndingItemViewModel(ItemshopEventDto dto)
    {
        Title = dto.Title;
        Ends = Remaining(dto.EndsAt);
    }

    public string Title { get; }
    public string Ends { get; }

    /// „endet heute", „noch 4 Tage" - genauer braucht es an dieser Stelle nicht
    /// zu sein, die Itemshop-Seite nennt den Zeitraum.
    private static string Remaining(DateTime? endsAt)
    {
        if (endsAt is not { } end) return "";

        var days = (end.ToLocalTime().Date - DateTime.Now.Date).Days;
        return days switch
        {
            <= 0 => Loc.T("start.itemshop.today"),
            1 => Loc.T("start.itemshop.tomorrow"),
            _ => Loc.T("start.itemshop.days", days),
        };
    }
}
