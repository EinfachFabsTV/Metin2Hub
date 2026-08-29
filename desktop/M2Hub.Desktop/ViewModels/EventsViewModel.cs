using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Ein globales Event (Forum-Board "News - Events").
public sealed class GlobalEventViewModel : ViewModelBase
{
    private Bitmap? _image;

    public GlobalEventViewModel(GlobalEventDto dto)
    {
        Dto = dto;
        Title = dto.Title;
        Kind = Services.Forum.Board.GlobalKindLabels.TryGetValue(dto.Kind, out var label) ? label : "Event";
        Parts = new ObservableCollection<string>(dto.Parts);
        Text = dto.BodyText.Trim();
        Url = dto.Url;

        ImageUrl = dto.ImageUrl;

        Range = FormatRange(dto.StartsAt, dto.EndsAt);

        var now = DateTime.Now;
        var start = dto.StartsAt?.ToLocalTime();
        var end = dto.EndsAt?.ToLocalTime();
        Running = start is not null && end is not null && now >= start && now <= end;
        Upcoming = start is not null && now < start;
        Over = end is not null && now > end;
        HasPeriod = start is not null || end is not null;
    }

    public GlobalEventDto Dto { get; }
    public string Title { get; }
    public string Kind { get; }
    public ObservableCollection<string> Parts { get; }
    public bool HasParts => Parts.Count > 0;
    public string Text { get; }
    public bool HasText => Text.Length > 0;
    public string Url { get; }
    public string? ImageUrl { get; }
    public string Range { get; }
    public bool Running { get; }
    public bool Upcoming { get; }
    public bool Over { get; }

    /// Ohne erkannten Zeitraum ist es meist gar kein Event, sondern ein
    /// Beitrag, der zufaellig im Event-Board steht.
    public bool HasPeriod { get; }

    /// Reihenfolge in der Liste: was laeuft, dann was kommt, dann der Rest.
    public int Rank => Running ? 0 : Upcoming ? 1 : 2;

    public Bitmap? Image { get => _image; private set { if (Set(ref _image, value)) Raise(nameof(HasImage)); } }
    public bool HasImage => _image is not null;

    public async Task LoadImageAsync(ImageCache cache) => Image = await cache.GetAsync(ImageUrl);

    public static string FormatRange(DateTime? from, DateTime? to)
    {
        if (from is null && to is null) return Loc.T("events.unknownPeriod");
        var f = from?.ToLocalTime();
        var t = to?.ToLocalTime();
        if (f is not null && t is not null) return $"{f:dd.MM.yyyy HH:mm} – {t:dd.MM.yyyy HH:mm}";
        if (f is not null) return Loc.T("events.from", $"{f:dd.MM.yyyy HH:mm}");
        return Loc.T("events.until", $"{t:dd.MM.yyyy HH:mm}");
    }
}

/// Eine Zelle im Serverkalender.
public sealed class CalCellViewModel
{
    public CalCellViewModel(string text, bool isLabel, bool active)
    {
        IsLabel = isLabel;
        Active = active;

        // Zu den Eventnamen gehoert ein Bild, sofern eines sicher genug passt.
        // Die Namen sind im Forum von Hand geschrieben - die Zuordnung haelt
        // deshalb Abkuerzungen und Tippfehler aus (siehe EventIcons).
        if (!isLabel) Icon = EventIcons.Find(text);

        // Zugeordnet wird ueber den deutschen Namen, angezeigt in der
        // eingestellten Sprache. Was das Verzeichnis nicht kennt, bleibt
        // deutsch stehen - lieber der Originaltext als eine erfundene
        // Uebersetzung.
        Text = Glossary.Term(text);
    }

    public string Text { get; }
    public bool IsLabel { get; }
    public bool Active { get; }
    public bool HasText => Text.Trim().Length > 0;

    public Bitmap? Icon { get; }
    public bool HasIcon => Icon is not null;
}

public sealed class CalRowViewModel
{
    public CalRowViewModel(IEnumerable<CalCellViewModel> cells, bool today)
    {
        Cells = new ObservableCollection<CalCellViewModel>(cells);
        Today = today;
    }

    public ObservableCollection<CalCellViewModel> Cells { get; }
    public bool Today { get; }
}

/// Kalender eines Servers bzw. einer Servergruppe.
public sealed class ServerCalViewModel
{
    public ServerCalViewModel(ServerCalDto dto, int today, int weekday, int hour)
    {
        Key = dto.Key;
        Label = dto.Label;
        CurrentIcon = dto.Current is null ? null : EventIcons.Find(dto.Current.Text);
        Columns = dto.Columns.Count + 1;
        Current = dto.Current;
        Specials = new ObservableCollection<string>(dto.Specials);
        CurrentLine = dto.Current is null ? "" : Loc.T("events.now", dto.Current.From, dto.Current.To);
        CurrentText = Glossary.Term(dto.Current?.Text);

        Header = new CalRowViewModel(
            new[] { new CalCellViewModel(
                Loc.T(dto.Type == "date" ? "events.column.day" : "events.column.weekday"), true, false) }
                .Concat(dto.Columns.Select(c => new CalCellViewModel(c.Label, true, false))),
            false);

        var rows = new List<CalRowViewModel>();
        foreach (var row in dto.Rows)
        {
            var isToday = dto.Type == "date" ? row.D == today : row.Weekday == weekday;
            var cells = new List<CalCellViewModel> { new(row.Label, true, isToday) };
            for (var i = 0; i < dto.Columns.Count; i++)
            {
                var text = i < row.Cells.Count ? row.Cells[i] : "";
                var col = dto.Columns[i];
                var active = isToday && hour >= col.From && hour < col.To;
                cells.Add(new CalCellViewModel(text, false, active));
            }
            rows.Add(new CalRowViewModel(cells, isToday));
        }
        Rows = new ObservableCollection<CalRowViewModel>(rows);
    }

    public string Key { get; }
    public string Label { get; }

    /// Bild zum gerade laufenden Event - dasselbe wie in der Zelle.
    public Bitmap? CurrentIcon { get; }
    public bool HasCurrentIcon => CurrentIcon is not null;

    public int Columns { get; }
    public CalRowViewModel Header { get; }
    public ObservableCollection<CalRowViewModel> Rows { get; }
    public ObservableCollection<string> Specials { get; }
    public bool HasSpecials => Specials.Count > 0;
    public CurrentDto? Current { get; }

    /// „Jetzt (16–20)" in der eingestellten Sprache.
    public string CurrentLine { get; }

    public string CurrentText { get; }
    public bool HasCurrent => Current is not null;
}

/// Ein Reiter der Seite: entweder die globale Liste oder ein Serverkalender.
/// Welche Server es gibt, steht erst nach dem Abruf fest - je Sprache und
/// Monat sind es andere. Deshalb werden die Reiter aufgebaut, nicht verdrahtet.
public sealed class EventTabViewModel
{
    public EventTabViewModel(string key, string label, ServerCalViewModel? server)
    {
        Key = key;
        Label = label;
        Server = server;
    }

    public string Key { get; }
    public string Label { get; }
    public ServerCalViewModel? Server { get; }
    public bool IsGlobal => Server is null;
    public bool IsServer => Server is not null;
}

/// Seite "Events": global und serverspezifisch, beides nur lesend.
public sealed class EventsViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly ForumService _forum;
    private readonly ImageCache _images;

    private bool _busy;
    private string? _error;
    private string _month = "";
    // Standard: abgelaufene Ankuendigungen ausblenden.
    private bool _onlyRunning = true;
    private bool _showUndated;
    private EventTabViewModel? _selectedTab;
    private string _selectedKey = GlobalTabKey;

    private const string GlobalTabKey = "__global";

    private readonly List<GlobalEventViewModel> _allGlobal = new();

    public EventsViewModel(LocalStore store, ForumService forum, ImageCache images)
    {
        _store = store;
        _forum = forum;
        _images = images;
        _showUndated = store.Settings.ShowUndatedEvents;
        ReloadCommand = new AsyncRelayCommand(_ => RefreshAsync());
        OpenInBrowserCommand = new RelayCommand(p => Platform.OpenUrl(p as string));
    }

    public bool Busy { get => _busy; private set { if (Set(ref _busy, value)) Raise(nameof(NotBusy)); } }
    public bool NotBusy => !_busy;
    public string? Error { get => _error; private set { if (Set(ref _error, value)) Raise(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(_error);

    public ObservableCollection<GlobalEventViewModel> GlobalEvents { get; } = new();
    public ObservableCollection<ServerCalViewModel> Servers { get; } = new();

    /// Erster Reiter ist immer "Global", danach je Server einer.
    public ObservableCollection<EventTabViewModel> Tabs { get; } = new();

    public EventTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (!Set(ref _selectedTab, value)) return;
            // Beim naechsten Aufbau soll derselbe Reiter wieder vorn stehen.
            if (value is not null) _selectedKey = value.Key;
        }
    }

    public string Month { get => _month; private set => Set(ref _month, value); }
    public bool Empty => !Busy && GlobalEvents.Count == 0 && Servers.Count == 0;

    /// Blendet abgelaufene Ankuendigungen aus. Was laeuft *und* was noch
    /// kommt, bleibt dabei immer sichtbar.
    public bool OnlyRunning
    {
        get => _onlyRunning;
        set { if (Set(ref _onlyRunning, value)) ApplyFilter(); }
    }

    /// Beitraege ohne erkannten Zeitraum. Im Event-Board stehen auch Dinge,
    /// die gar kein Event sind - Urlaubshinweise, Wortmeldungen zum Kalender.
    /// Die Website hat sie deshalb gar nicht erst gezeigt; hier lassen sie
    /// sich einblenden, falls doch einmal eine Ankuendigung darunter ist.
    public bool ShowUndated
    {
        get => _showUndated;
        set
        {
            if (!Set(ref _showUndated, value)) return;
            _store.Settings.ShowUndatedEvents = value;
            _store.SaveSettings();
            ApplyFilter();
        }
    }

    public AsyncRelayCommand ReloadCommand { get; }
    public RelayCommand OpenInBrowserCommand { get; }

    /// Zeigt den lokalen Stand an; geladen wird ausschliesslich ueber den
    /// Zeitplan bzw. den Knopf "Jetzt laden".
    public void Reload()
    {
        var now = Services.Forum.Html.BerlinNow();
        var cache = _store.Cache;

        _allGlobal.Clear();
        foreach (var dto in cache.GlobalEvents.OrderByDescending(e => e.StartsAt ?? e.FetchedAt ?? DateTime.MinValue))
            _allGlobal.Add(new GlobalEventViewModel(dto));

        // Der Monat des Kalenders, in der eingestellten Sprache.
        Month = Loc.T($"month.{now.M}");

        Servers.Clear();
        Tabs.Clear();
        Tabs.Add(new EventTabViewModel(GlobalTabKey, Loc.T("events.tab.global"), null));

        foreach (var dto in cache.Servers)
        {
            // Wer nur auf einem Server spielt, braucht die anderen Reiter nicht.
            if (ServerCatalog.IsHidden(_store, dto.Key)) continue;

            dto.Current = Services.Forum.EventCalendar.CurrentFor(dto, now);
            var cal = new ServerCalViewModel(dto, now.D, now.Weekday, now.Hour);
            Servers.Add(cal);
            Tabs.Add(new EventTabViewModel(dto.Key, dto.Label, cal));
        }

        SelectedTab = Tabs.FirstOrDefault(t => t.Key == _selectedKey) ?? Tabs[0];

        Error = cache.LastError;
        ApplyFilter();

        foreach (var ev in _allGlobal) _ = ev.LoadImageAsync(_images);
    }

    /// Manuelles Laden ueber den Knopf - erzwingt den Abruf trotz Zwischenspeicher.
    private async Task RefreshAsync()
    {
        Busy = true;
        try
        {
            await _forum.RefreshAsync(force: true);
        }
        finally
        {
            Busy = false;
        }
        Reload();
    }

    private void ApplyFilter()
    {
        GlobalEvents.Clear();
        foreach (var ev in _allGlobal.OrderBy(e => e.Rank).ThenBy(e => e.Dto.StartsAt ?? DateTime.MaxValue))
        {
            // Der Haken blendet nur Abgelaufenes aus - Kommendes bleibt stehen.
            if (OnlyRunning && ev.Over) continue;
            if (!ShowUndated && !ev.HasPeriod) continue;
            GlobalEvents.Add(ev);
        }
        Raise(nameof(Empty));
    }
}
