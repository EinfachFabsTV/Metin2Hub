using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Forum;

namespace M2Hub.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly ForumService _forum;
    private readonly UpdateService _updates;
    private readonly DispatcherTimer _timer;

    /// Laufender Betrieb: alle fuenf Minuten.
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// Zusaetzlich fest um 18:01 und 18:02 (Berliner Zeit) - dann wechseln im
    /// Forum die Tagesaktionen.
    private static readonly (int Hour, int Minute)[] FixedTimes = [(18, 1), (18, 2)];

    private DateTime _lastRun = DateTime.MinValue;
    private readonly HashSet<string> _firedSlots = new();

    private object? _currentPage;
    private string _currentKey = "events";
    private bool _busy;

    public MainWindowViewModel(LocalStore store, ForumService forum, DialogHost dialogs)
    {
        _store = store;
        _forum = forum;
        Dialogs = dialogs;

        var images = new ImageCache();
        _updates = new UpdateService();

        Accounts = new AccountsViewModel(store, dialogs);
        Events = new EventsViewModel(store, forum, images);
        Itemshop = new ItemshopViewModel(store, forum, images);
        Settings = new SettingsViewModel(
            store, dialogs, _updates, RefreshActiveNow, ReloadPages,
            info => dialogs.ShowAsync(new UpdateDialogViewModel(_updates, info, Restart)));

        ShowAccountsCommand = new RelayCommand(_ => Show("accounts"));
        ShowEventsCommand = new RelayCommand(_ => Show("events"));
        ShowItemshopCommand = new RelayCommand(_ => Show("itemshop"));
        ShowSettingsCommand = new RelayCommand(_ => Show("settings"));
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(manual: true));
        OpenLinkCommand = new RelayCommand(p => Platform.OpenUrl(p as string));

        // Nach einem Sprachwechsel muessen die Listen neu beschriftet werden -
        // sie tragen ihre Texte als feste Zeichenketten, nicht als Bindung.
        Loc.I.PropertyChanged += (_, _) =>
        {
            Accounts.RelabelAfterLanguageChange();
            Events.Reload();
            Itemshop.Reload();
        };

        // Der Wecker tickt haeufiger als der Abruf; entschieden wird in Tick().
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _timer.Tick += (_, _) => Tick();
    }

    /// Was gerade laeuft - steht oben links in der Kopfzeile.
    public ObservableCollection<ActiveBadgeViewModel> ActiveNow { get; } = new();
    public bool HasActiveNow => ActiveNow.Count > 0;

    /// Haelt die gerade offene Maske; die Ansicht legt sie ueber den Inhalt.
    public DialogHost Dialogs { get; }

    public AccountsViewModel Accounts { get; }
    public EventsViewModel Events { get; }
    public ItemshopViewModel Itemshop { get; }
    public SettingsViewModel Settings { get; }

    public object? CurrentPage { get => _currentPage; private set => Set(ref _currentPage, value); }

    public string CurrentKey
    {
        get => _currentKey;
        private set
        {
            if (!Set(ref _currentKey, value)) return;
            Raise(nameof(IsAccounts));
            Raise(nameof(IsEvents));
            Raise(nameof(IsItemshop));
            Raise(nameof(IsSettings));
        }
    }

    public bool IsAccounts => _currentKey == "accounts";
    public bool IsEvents => _currentKey == "events";
    public bool IsItemshop => _currentKey == "itemshop";
    public bool IsSettings => _currentKey == "settings";

    public bool Busy { get => _busy; private set { if (Set(ref _busy, value)) Raise(nameof(NotBusy)); } }
    public bool NotBusy => !_busy;

    /// Wann zuletzt geladen wurde - steht in der Kopfzeile.
    public string LastRefreshLabel
    {
        get
        {
            if (Busy) return Loc.T("header.loading");
            if (_store.Cache.LastRefreshAt is not { } at) return Loc.T("header.notLoaded");
            var local = at.ToLocalTime();
            return Loc.T("header.lastLoaded",
                local.Date == DateTime.Now.Date ? local.ToString("HH:mm") : local.ToString("dd.MM. HH:mm"));
        }
    }

    public string? RefreshError => _store.Cache.LastError;
    public bool HasRefreshError => !string.IsNullOrWhiteSpace(_store.Cache.LastError);

    public RelayCommand ShowAccountsCommand { get; }
    public RelayCommand ShowEventsCommand { get; }
    public RelayCommand ShowItemshopCommand { get; }
    public RelayCommand ShowSettingsCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    /// Oeffnet eine Adresse im Standardbrowser. Die App selbst zeigt keine
    /// fremden Seiten an - sie hat dafuer bewusst keine Browser-Engine.
    public RelayCommand OpenLinkCommand { get; }

    /// Nachschlagewerk und Handelsglas, beide ausserhalb der App.
    public const string WikiUrl = "https://metin2alerts.com/wiki";
    public const string TradingGlassUrl = "https://metin2alerts.com/store/";

    /// Beim Start: gespeicherten Stand zeigen, dann laden.
    public async Task InitializeAsync()
    {
        Accounts.EnsureLoaded();
        Events.Reload();
        Itemshop.Reload();
        RefreshActiveNow();
        Show("events");

        _timer.Start();
        await RefreshAsync(manual: false);
        await CheckUpdateAsync();
    }

    /// Hinweis auf eine neuere Version, hoechstens einmal je Version.
    private async Task CheckUpdateAsync()
    {
        if (!_store.Settings.CheckUpdates) return;

        var info = await _updates.CheckAsync();
        if (info is null || info.Version == _store.Settings.SkippedVersion) return;

        var ok = await Dialogs.ShowAsync(new UpdateDialogViewModel(_updates, info, Restart));

        if (!ok)
        {
            // Abgelehnt: bis zur naechsten Version Ruhe geben.
            _store.Settings.SkippedVersion = info.Version;
            _store.SaveSettings();
        }
    }

    /// Beide Seiten aus der Ablage neu aufbauen - nach dem Leeren des
    /// Zwischenspeichers und nach jedem Abruf.
    private void ReloadPages()
    {
        Events.Reload();
        Itemshop.Reload();
        // Welche Server es gibt, steht erst nach dem Abruf fest - Einstellungen
        // und Accounts bauen ihre Listen daraus auf.
        Settings.RefreshServers();
        Accounts.RefreshServers();
        RefreshActiveNow();
        Raise(nameof(LastRefreshLabel));
    }

    /// Der Nachfolger laeuft bereits - dieses Programm macht Platz.
    private void Restart()
    {
        Shutdown();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Environment.Exit(0);
    }

    public void Shutdown() => _timer.Stop();

    /// Reihenfolge der Reiter fuer die Pfeiltasten.
    private static readonly string[] PageOrder = ["accounts", "events", "itemshop", "settings"];

    /// Zum Nachbarreiter springen; am Ende geht es vorn weiter.
    public void ShowNeighbour(int step)
    {
        var at = Array.IndexOf(PageOrder, _currentKey);
        if (at < 0) at = 0;
        var next = ((at + step) % PageOrder.Length + PageOrder.Length) % PageOrder.Length;
        Show(PageOrder[next]);
    }

    private void Show(string key)
    {
        CurrentKey = key;
        CurrentPage = key switch
        {
            "accounts" => Accounts,
            "itemshop" => Itemshop,
            "settings" => Settings,
            _ => Events,
        };
        if (key == "accounts") Accounts.EnsureLoaded();
    }

    /// Globale Events und Happy Hours, die gerade laufen. Beides steht in der
    /// Kopfzeile, damit es auch sichtbar ist, wenn man gerade bei den Accounts
    /// ist. Endet etwas, verschwindet der Hinweis beim naechsten Tick.
    private void RefreshActiveNow()
    {
        var now = DateTime.Now;

        var wanted = new List<(string Title, DateTime? EndsAt, bool HappyHour)>();

        foreach (var e in _store.Cache.GlobalEvents)
        {
            if (!IsRunning(e.StartsAt, e.EndsAt, now)) continue;
            wanted.Add((e.Title, e.EndsAt, false));
        }

        foreach (var e in _store.Cache.Itemshop)
        {
            // Aus dem Itemshop interessiert an dieser Stelle nur die Happy Hour.
            if (e.Kind != "happyhour") continue;
            if (!IsRunning(e.StartsAt, e.EndsAt, now)) continue;
            wanted.Add((e.Title, e.EndsAt, true));
        }

        // Was zuerst endet, steht vorn - das ist das Eilige.
        wanted.Sort((a, b) => (a.EndsAt ?? DateTime.MaxValue).CompareTo(b.EndsAt ?? DateTime.MaxValue));

        // Unveraendert? Dann die Liste in Ruhe lassen, sonst flackert sie alle
        // zwanzig Sekunden.
        if (wanted.Count == ActiveNow.Count &&
            wanted.Select(w => w.Title).SequenceEqual(ActiveNow.Select(b => b.Title)))
            return;

        ActiveNow.Clear();

        // Auf Wunsch steht das laufende Event eines Serverkalenders mit oben.
        if (CurrentServerEvent() is { } server)
            ActiveNow.Add(new ActiveBadgeViewModel(
                Glossary.Term(server.Text), null, false, ShowEventsCommand,
                Loc.T("events.now", server.From, server.To), EventIcons.Find(server.Text)));

        foreach (var (title, endsAt, happyHour) in wanted)
        {
            var open = happyHour ? ShowItemshopCommand : ShowEventsCommand;
            ActiveNow.Add(new ActiveBadgeViewModel(title, endsAt, happyHour, open));
        }
        Raise(nameof(HasActiveNow));
    }

    /// Das gerade laufende Event des in den Einstellungen gewaehlten Servers.
    private Models.CurrentDto? CurrentServerEvent()
    {
        var key = _store.Settings.HeaderServer;
        if (string.IsNullOrEmpty(key)) return null;

        var dto = _store.Cache.Servers.FirstOrDefault(s => s.Key == key);
        return dto is null ? null : EventCalendar.CurrentFor(dto, Html.BerlinNow());
    }

    private static bool IsRunning(DateTime? startsAt, DateTime? endsAt, DateTime now) =>
        startsAt is { } s && endsAt is { } e && now >= s.ToLocalTime() && now <= e.ToLocalTime();

    /* ---------- Zeitplan ---------- */

    private void Tick()
    {
        // Laufzeiten enden auch ohne neuen Abruf - deshalb bei jedem Tick pruefen.
        RefreshActiveNow();

        var berlin = Html.ToBerlin(DateTime.UtcNow);

        // Feste Zeiten: je Tag und Minute genau einmal
        foreach (var (hour, minute) in FixedTimes)
        {
            if (berlin.Hour != hour || berlin.Minute != minute) continue;
            var slot = $"{berlin:yyyy-MM-dd} {hour:00}:{minute:00}";
            if (_firedSlots.Add(slot))
            {
                // Der Satz bleibt klein; alte Tage werden nicht gebraucht.
                if (_firedSlots.Count > 8) _firedSlots.Clear();
                _ = RefreshAsync(manual: false);
                return;
            }
        }

        if (DateTime.UtcNow - _lastRun >= Interval) _ = RefreshAsync(manual: false);
    }

    private async Task RefreshAsync(bool manual)
    {
        if (Busy) return;

        _lastRun = DateTime.UtcNow;
        Busy = true;
        Raise(nameof(LastRefreshLabel));
        try
        {
            // manual erzwingt den Abruf auch dann, wenn der Zwischenspeicher
            // noch als frisch gilt.
            await _forum.RefreshAsync(force: manual);
        }
        finally
        {
            Busy = false;
        }

        ReloadPages();
        Raise(nameof(RefreshError));
        Raise(nameof(HasRefreshError));
    }
}
