using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Ein Eintrag der Serverauswahl fuer die Kopfzeile.
public sealed record HeaderServerOption(string Key, string Label);

/// Ein Bereich, der beim Start geoeffnet werden kann.
public sealed record StartPageOption(string Key, string Label);

/// Eine Zeile der Abschnittsliste: Haken und Beschriftung. Das Umschalten
/// meldet sich sofort, damit die Startseite nicht erst beim Verlassen der
/// Einstellungen nachzieht.
public sealed class DashboardSectionRow : ViewModelBase
{
    private readonly Action<DashboardSectionRow> _changed;
    private bool _visible;

    public DashboardSectionRow(string key, bool visible, Action<DashboardSectionRow> changed)
    {
        Key = key;
        _visible = visible;
        _changed = changed;
        Label = DashboardSections.Label(key);
    }

    public string Key { get; }
    public string Label { get; }

    public bool Visible
    {
        get => _visible;
        set { if (Set(ref _visible, value)) _changed(this); }
    }
}

/// Ein Server mit dem Haken, ob er angezeigt wird.
public sealed class ServerVisibility : ViewModelBase
{
    private readonly Action<string, bool> _changed;
    private bool _visible;

    public ServerVisibility(string key, string label, bool visible, Action<string, bool> changed)
    {
        Key = key;
        _label = label;
        _visible = visible;
        _changed = changed;
    }

    public string Key { get; }

    private string _label;
    public string Label { get => _label; set => Set(ref _label, value); }

    public bool Visible
    {
        get => _visible;
        set { if (Set(ref _visible, value)) _changed(Key, value); }
    }
}

/// Ein Eintrag der Sprachauswahl. "auto" folgt Windows.
public sealed record LanguageOption(string Code, string Label);

/// Eine Sortierart der Accounts: gespeichert wird der Schluessel, angezeigt
/// der uebersetzte Text.
public sealed record SortOption(string Key, string Label);

/// Einstellungen der App. Alles hier ist lokal und sofort wirksam.
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly IDialogService _dialogs;
    private readonly UpdateService _updates;
    private readonly StreamOverlay _stream;
    private bool _allPatchnotes;
    private bool _sectionsOpen;
    private bool _streamOpen;
    private bool _notesOpen;
    private readonly Action _headerChanged;
    private readonly Action _cacheCleared;
    private readonly Func<UpdateService.UpdateInfo, Task> _showUpdate;

    private LanguageOption _language;
    private HeaderServerOption _headerServer;
    private bool _checkUpdates;
    private bool _teamPostsOnly;
    private StartPageOption _startPage;

    /// Betriebsart, fuer die die Abschnittsliste gerade gebaut ist.
    private string _sectionsMode = "";
    private string _teamNames;
    private string? _status;
    private bool _busy;

    public SettingsViewModel(
        LocalStore store,
        IDialogService dialogs,
        UpdateService updates,
        StreamOverlay stream,
        Action headerChanged,
        Action cacheCleared,
        Func<UpdateService.UpdateInfo, Task> showUpdate)
    {
        _store = store;
        _dialogs = dialogs;
        _updates = updates;
        _stream = stream;
        _headerChanged = headerChanged;
        _cacheCleared = cacheCleared;
        _showUpdate = showUpdate;

        _checkUpdates = store.Settings.CheckUpdates;
        _teamPostsOnly = store.Settings.TeamPostsOnly;

        StartPages = BuildStartPages();
        _startPage = StartPages.FirstOrDefault(o => o.Key == store.Settings.StartPage)
                     ?? StartPages[0];
        RebuildSections();
        MoveSectionCommand = new RelayCommand(p => MoveSection(p as DashboardSectionRow, -1));
        MoveSectionDownCommand = new RelayCommand(p => MoveSection(p as DashboardSectionRow, +1));
        _teamNames = string.Join(", ", store.Settings.TeamNames);
        _language = LanguageOptions.FirstOrDefault(o => o.Code == store.Settings.Language)
                    ?? LanguageOptions[0];
        HeaderServers = BuildHeaderServers(store);
        Servers = BuildServerRows(store);
        _headerServer = HeaderServers.FirstOrDefault(o => o.Key == store.Settings.HeaderServer)
                        ?? HeaderServers[0];

        ToggleSectionsCommand = new RelayCommand(_ => SectionsOpen = !SectionsOpen);
        ToggleStreamCommand = new RelayCommand(_ => StreamOpen = !StreamOpen);
        ToggleNotesCommand = new RelayCommand(_ => NotesOpen = !NotesOpen);

        StreamScopes = BuildStreamScopes();
        GoalSizes = BuildGoalSizes();
        ChooseGoalSizeCommand = new RelayCommand(p => { if (p is RunChip o) GoalSize = o; });
        ChooseStreamScopeCommand = new RelayCommand(p => { if (p is RunChip o) StreamScope = o; });
        ResetStreamStartCommand = new RelayCommand(_ => ResetStreamStart());
        OpenStreamFolderCommand = new RelayCommand(_ => Platform.OpenFolder(StreamOverlay.FolderPath));

        OpenFolderCommand = new RelayCommand(_ => Platform.OpenFolder(LocalStore.Directory));
        OpenReleasesCommand = new RelayCommand(_ => Platform.OpenUrl(UpdateService.ReleasePage));
        CheckNowCommand = new AsyncRelayCommand(_ => CheckNowAsync());
        ClearCacheCommand = new AsyncRelayCommand(_ => ClearCacheAsync());
    }

    /// Sprache der Oberflaeche. „Automatisch" folgt Windows.
    public ObservableCollection<LanguageOption> LanguageOptions { get; } = BuildLanguages();

    private static ObservableCollection<LanguageOption> BuildLanguages()
    {
        var list = new ObservableCollection<LanguageOption>
        {
            new("auto", Loc.T("settings.language.auto")),
        };
        foreach (var code in Loc.Languages)
            list.Add(new LanguageOption(code, Loc.LanguageNames[code]));
        return list;
    }

    public LanguageOption Language
    {
        get => _language;
        set
        {
            if (!Set(ref _language, value)) return;
            _store.Settings.Language = value?.Code ?? "auto";
            _store.SaveSettings();
            // Wirkt sofort - die Ansichten haengen am Indexer von Loc.
            Loc.I.SetLanguage(_store.Settings.Language);
            Raise(nameof(Version));
        }
    }

    /// Welcher Serverkalender sein laufendes Event oben mitzeigt. Die Auswahl
    /// stammt aus dem zuletzt geladenen Kalender - je Sprache und Monat gibt es
    /// andere Server, eine feste Liste waere schnell falsch.
    public ObservableCollection<HeaderServerOption> HeaderServers { get; }

    private static ObservableCollection<HeaderServerOption> BuildHeaderServers(LocalStore store)
    {
        var list = new ObservableCollection<HeaderServerOption>
        {
            new("", Loc.T("settings.header.none")),
        };
        foreach (var server in store.Cache.Servers)
        {
            // Ein ausgeblendeter Server gehoert auch nicht in die Kopfzeile.
            if (ServerCatalog.IsHidden(store, server.Key)) continue;
            list.Add(new HeaderServerOption(server.Key, server.Label));
        }

        // Der gewaehlte Server steht noch nicht im Zwischenspeicher (erster
        // Start, Abruf steht aus) - die Einstellung darf trotzdem nicht
        // stillschweigend verfallen.
        var chosen = store.Settings.HeaderServer;
        if (chosen.Length > 0 && list.All(o => o.Key != chosen))
            list.Add(new HeaderServerOption(chosen, chosen));

        return list;
    }

    /// Welche Kalender ueberhaupt angezeigt werden - Chimera, Oceana und Blos
    /// teilen sich einen, deshalb steht hier ein Haken fuer alle drei. Ein
    /// abgewaehlter Kalender
    /// verschwindet aus den Event-Reitern und aus der Auswahl bei den Accounts;
    /// seine Daten bleiben unberuehrt.
    public ObservableCollection<ServerVisibility> Servers { get; }

    public bool HasServers => Servers.Count > 0;

    /// Nach einem Abruf koennen neue Server dazugekommen sein.
    public void RefreshServers()
    {
        var known = ServerCatalog.Calendars(_store);
        if (known.Count == Servers.Count && known.All(s => Servers.Any(r => r.Key == s.Key)))
        {
            foreach (var row in Servers)
                row.Label = known.First(s => s.Key == row.Key).Label;
            return;
        }

        Servers.Clear();
        foreach (var server in known)
            Servers.Add(new ServerVisibility(server.Key, server.Label,
                !ServerCatalog.IsHidden(_store, server.Key), SetServerVisible));
        Raise(nameof(HasServers));
    }

    private ObservableCollection<ServerVisibility> BuildServerRows(LocalStore store)
    {
        var rows = new ObservableCollection<ServerVisibility>();
        foreach (var server in ServerCatalog.Calendars(store))
            rows.Add(new ServerVisibility(server.Key, server.Label,
                !ServerCatalog.IsHidden(store, server.Key), SetServerVisible));
        return rows;
    }

    private void SetServerVisible(string key, bool visible)
    {
        var hidden = _store.Settings.HiddenServers;
        if (visible) hidden.RemoveAll(k => string.Equals(k, key, StringComparison.Ordinal));
        else if (!hidden.Contains(key, StringComparer.Ordinal)) hidden.Add(key);

        _store.SaveSettings();
        // Events und Accounts bauen ihre Listen daraus auf - sofort wirksam.
        _cacheCleared();
    }

    public HeaderServerOption HeaderServer
    {
        get => _headerServer;
        set
        {
            if (!Set(ref _headerServer, value)) return;
            _store.Settings.HeaderServer = value?.Key ?? "";
            _store.SaveSettings();
            _headerChanged();
        }
    }

    public bool CheckUpdates
    {
        get => _checkUpdates;
        set
        {
            if (!Set(ref _checkUpdates, value)) return;
            _store.Settings.CheckUpdates = value;
            _store.SaveSettings();
        }
    }

    /* ---------- Startseite ---------- */

    /// Die Abschnitte der Startseite in ihrer Reihenfolge, samt Haken. Welche
    /// Anordnung bearbeitet wird, sagt die Betriebsart, die auf der Startseite
    /// eingestellt ist - hier steht nur, dass sie es ist.
    public ObservableCollection<DashboardSectionRow> Sections { get; } = new();

    public RelayCommand MoveSectionCommand { get; private set; } = null!;
    public RelayCommand MoveSectionDownCommand { get; private set; } = null!;

    public string EditingMode => Loc.T("settings.start.editing",
        Loc.T(_store.Settings.DashboardMode == DashboardSections.Work
            ? "start.mode.work"
            : "start.mode.overview"));

    /// Welcher Bereich beim Start geoeffnet wird.
    public List<StartPageOption> StartPages { get; private set; } = new();

    public StartPageOption StartPage
    {
        get => _startPage;
        set
        {
            if (!Set(ref _startPage, value)) return;
            _store.Settings.StartPage = value?.Key ?? "start";
            _store.SaveSettings();
        }
    }

    private static List<StartPageOption> BuildStartPages() =>
    [
        new("start", Loc.T("nav.start")),
        new("accounts", Loc.T("nav.accounts")),
        new("runs", Loc.T("nav.runs")),
        new("goals", Loc.T("nav.goals")),
        new("events", Loc.T("nav.events")),
        new("itemshop", Loc.T("nav.itemshop")),
        new("calc", Loc.T("nav.calc")),
    ];

    /// Die gespeicherte Reihenfolge, um alles ergaenzt, was noch fehlt - so
    /// stehen auch abgewaehlte Abschnitte in der Liste.
    /// Nach einem Wechsel der Betriebsart auf der Startseite: die Liste zeigt
    /// dann die andere Anordnung. Waehrend des Anklickens wird sie nicht neu
    /// gebaut - sonst sprangen die Zeilen unter der Maus weg.
    public void RefreshSections()
    {
        if (_sectionsMode == _store.Settings.DashboardMode) return;
        RebuildSections();
    }

    private void RebuildSections()
    {
        _sectionsMode = _store.Settings.DashboardMode;
        var chosen = Current();

        Sections.Clear();
        foreach (var key in chosen)
            Sections.Add(new DashboardSectionRow(key, true, OnSectionToggled));

        foreach (var key in DashboardSections.All)
            if (!chosen.Contains(key))
                Sections.Add(new DashboardSectionRow(key, false, OnSectionToggled));

        Raise(nameof(EditingMode));
    }

    private List<string> Current()
    {
        var saved = _store.Settings.DashboardMode == DashboardSections.Work
            ? _store.Settings.DashboardWork
            : _store.Settings.DashboardOverview;

        return saved.Count == 0
            ? [.. DashboardSections.Default(_store.Settings.DashboardMode)]
            : saved.Where(k => DashboardSections.All.Contains(k)).ToList();
    }

    private void OnSectionToggled(DashboardSectionRow _) => SaveSections();

    private void MoveSection(DashboardSectionRow? row, int step)
    {
        if (row is null) return;

        var at = Sections.IndexOf(row);
        var to = at + step;
        if (at < 0 || to < 0 || to >= Sections.Count) return;

        Sections.Move(at, to);
        SaveSections();
    }

    private void SaveSections()
    {
        var chosen = Sections.Where(r => r.Visible).Select(r => r.Key).ToList();

        if (_store.Settings.DashboardMode == DashboardSections.Work)
            _store.Settings.DashboardWork = chosen;
        else
            _store.Settings.DashboardOverview = chosen;

        _store.SaveSettings();
        _cacheCleared();
    }

    /// Im Itemshop stehen gelegentlich Beitraege von Spielern. Gefiltert wird
    /// ueber die Namen der Team-Accounts; solange keine hinterlegt sind,
    /// bleibt alles sichtbar.
    public bool TeamPostsOnly
    {
        get => _teamPostsOnly;
        set
        {
            if (!Set(ref _teamPostsOnly, value)) return;
            _store.Settings.TeamPostsOnly = value;
            _store.SaveSettings();
            _cacheCleared();
        }
    }

    /// Die Namen als eine Zeile, durch Komma getrennt - fuer eine Handvoll
    /// Eintraege braucht es keine Liste mit Knoepfen.
    public string TeamNames
    {
        get => _teamNames;
        set
        {
            if (!Set(ref _teamNames, value)) return;
            _store.Settings.TeamNames = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            _store.SaveSettings();
            _cacheCleared();
        }
    }

    public string Version => Loc.T("settings.version", UpdateService.CurrentVersion);
    public string DataFolder => LocalStore.Directory;

    public bool Busy { get => _busy; private set { if (Set(ref _busy, value)) Raise(nameof(NotBusy)); } }
    public bool NotBusy => !_busy;

    public string? Status { get => _status; private set { if (Set(ref _status, value)) Raise(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrWhiteSpace(_status);

    /* ---------- Stream-Einblendung ---------- */

    /// Die drei Zeitraeume als Knopfreihe, wie bei den Laeufen.
    public ObservableCollection<RunChip> StreamScopes { get; }

    public RunChip StreamScope
    {
        get => StreamScopes.FirstOrDefault(o => o.Key == _store.Settings.Stream.Scope)
               ?? StreamScopes[0];
        set
        {
            if (value is null || _store.Settings.Stream.Scope == value.Key) return;

            _store.Settings.Stream.Scope = value.Key;
            foreach (var o in StreamScopes) o.IsActive = o.Key == value.Key;
            _store.SaveSettings();
            _stream.Write();
            Raise(nameof(StreamScope));
        }
    }

    /// Schreibt die App die Dateien? Beim Einschalten stehen sie sofort da -
    /// sonst richtet man die Quelle in OBS auf eine Datei, die es nicht gibt.
    public bool StreamEnabled
    {
        get => _store.Settings.Stream.Enabled;
        set
        {
            if (_store.Settings.Stream.Enabled == value) return;

            _store.Settings.Stream.Enabled = value;
            _store.SaveSettings();
            _stream.Write();
            Raise(nameof(StreamEnabled));
        }
    }

    /// Die zweite Einblendung samt ihren Stuecken. Jedes einzeln, weil in
    /// einem Overlay jede Zeile Platz kostet.
    public bool GoalOverlayEnabled
    {
        get => _store.Settings.Stream.GoalEnabled;
        set => SetStream(v => _store.Settings.Stream.GoalEnabled = v, value,
            _store.Settings.Stream.GoalEnabled, nameof(GoalOverlayEnabled));
    }

    public bool GoalShowProgress
    {
        get => _store.Settings.Stream.GoalShowProgress;
        set => SetStream(v => _store.Settings.Stream.GoalShowProgress = v, value,
            _store.Settings.Stream.GoalShowProgress, nameof(GoalShowProgress));
    }

    public bool GoalShowNet
    {
        get => _store.Settings.Stream.GoalShowNet;
        set => SetStream(v => _store.Settings.Stream.GoalShowNet = v, value,
            _store.Settings.Stream.GoalShowNet, nameof(GoalShowNet));
    }

    public bool GoalShowRuns
    {
        get => _store.Settings.Stream.GoalShowRuns;
        set => SetStream(v => _store.Settings.Stream.GoalShowRuns = v, value,
            _store.Settings.Stream.GoalShowRuns, nameof(GoalShowRuns));
    }

    public bool GoalShowChests
    {
        get => _store.Settings.Stream.GoalShowChests;
        set => SetStream(v => _store.Settings.Stream.GoalShowChests = v, value,
            _store.Settings.Stream.GoalShowChests, nameof(GoalShowChests));
    }

    public bool GoalShowActiveRun
    {
        get => _store.Settings.Stream.GoalShowActiveRun;
        set => SetStream(v => _store.Settings.Stream.GoalShowActiveRun = v, value,
            _store.Settings.Stream.GoalShowActiveRun, nameof(GoalShowActiveRun));
    }

    /// Setzen, speichern, Dateien neu schreiben - fuer alle Schalter derselbe
    /// Weg.
    private void SetStream(Action<bool> set, bool value, bool current, string name)
    {
        if (current == value) return;

        set(value);
        _store.SaveSettings();
        _stream.Write();
        Raise(name);
    }

    /// Die Schriftgroesse der Goal-Leiste - drei Stufen, damit man die Karte
    /// nicht in OBS kleinziehen muss.
    public ObservableCollection<RunChip> GoalSizes { get; private set; } = new();

    public RunChip GoalSize
    {
        get => GoalSizes.FirstOrDefault(o => o.Key == _store.Settings.Stream.GoalSize)
               ?? GoalSizes[0];
        set
        {
            if (value is null || _store.Settings.Stream.GoalSize == value.Key) return;

            _store.Settings.Stream.GoalSize = value.Key;
            foreach (var o in GoalSizes) o.IsActive = o.Key == value.Key;
            _store.SaveSettings();
            _stream.Write();
            Raise(nameof(GoalSize));
        }
    }

    public RelayCommand ChooseGoalSizeCommand { get; private set; } = new(_ => { });

    private ObservableCollection<RunChip> BuildGoalSizes()
    {
        var list = new ObservableCollection<RunChip>
        {
            new("s", Loc.T("settings.stream.goal.size.s")),
            new("m", Loc.T("settings.stream.goal.size.m")),
            new("l", Loc.T("settings.stream.goal.size.l")),
        };
        foreach (var o in list) o.IsActive = o.Key == _store.Settings.Stream.GoalSize;
        return list;
    }

    public RelayCommand ChooseStreamScopeCommand { get; }
    public RelayCommand ResetStreamStartCommand { get; }
    public RelayCommand OpenStreamFolderCommand { get; }

    private ObservableCollection<RunChip> BuildStreamScopes()
    {
        var list = new ObservableCollection<RunChip>
        {
            new(StreamOverlay.Today, Loc.T("stream.scope.today")),
            new(StreamOverlay.Session, Loc.T("stream.scope.session")),
            new(StreamOverlay.Total, Loc.T("stream.scope.total")),
        };
        foreach (var o in list) o.IsActive = o.Key == _store.Settings.Stream.Scope;
        return list;
    }

    private void ResetStreamStart()
    {
        _store.Settings.Stream.SessionStart = DateTime.Now;
        _store.SaveSettings();
        _stream.Write();
        Status = Loc.T("settings.stream.resetDone");
    }

    /* ---------- Aufklapper ---------- */

    /// Die langen Abschnitte stehen zugeklappt da. Gebaut aus denselben
    /// Teilen wie der Rest - ein `Expander` bringt sein eigenes Aussehen mit
    /// und stand fremd zwischen den Karten.
    public bool SectionsOpen
    {
        get => _sectionsOpen;
        set { if (Set(ref _sectionsOpen, value)) Raise(nameof(SectionsChevron)); }
    }

    public bool StreamOpen
    {
        get => _streamOpen;
        set { if (Set(ref _streamOpen, value)) Raise(nameof(StreamChevron)); }
    }

    public bool NotesOpen
    {
        get => _notesOpen;
        set { if (Set(ref _notesOpen, value)) Raise(nameof(NotesChevron)); }
    }

    public string SectionsChevron => _sectionsOpen ? "▾" : "▸";
    public string StreamChevron => _streamOpen ? "▾" : "▸";
    public string NotesChevron => _notesOpen ? "▾" : "▸";

    public RelayCommand ToggleSectionsCommand { get; private set; } = new(_ => { });
    public RelayCommand ToggleStreamCommand { get; private set; } = new(_ => { });
    public RelayCommand ToggleNotesCommand { get; private set; } = new(_ => { });

    /* ---------- Patchnotes ---------- */

    /// Was sich in dieser Fassung geaendert hat. Die Liste steht in
    /// `desktop/CHANGELOG.md` und liegt der App als Datei bei.
    public string Patchnotes { get; } = Services.Patchnotes.Latest();

    public bool HasPatchnotes => Patchnotes.Length > 0;

    public bool AllPatchnotes
    {
        get => _allPatchnotes;
        set
        {
            if (!Set(ref _allPatchnotes, value)) return;

            Raise(nameof(PatchnotesText));
        }
    }

    /// Standard ist die neueste Fassung; wer mehr sehen will, klappt alles auf.
    public string PatchnotesText =>
        _allPatchnotes ? Services.Patchnotes.Everything() : Patchnotes;

    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenReleasesCommand { get; }
    public AsyncRelayCommand CheckNowCommand { get; }
    public AsyncRelayCommand ClearCacheCommand { get; }

    private async Task CheckNowAsync()
    {
        Busy = true;
        Status = null;
        try
        {
            var info = await _updates.CheckAsync();
            Status = info is null
                ? Loc.T("settings.update.none", UpdateService.CurrentVersion)
                : Loc.T("update.title", info.Version);

            if (info is not null) await _showUpdate(info);
        }
        finally
        {
            Busy = false;
        }
    }

    private async Task ClearCacheAsync()
    {
        var ok = await _dialogs.ConfirmAsync(
            Loc.T("settings.data.clear"),
            Loc.T("settings.data.clearHint"),
            Loc.T("settings.data.clearOk"));
        if (!ok) return;

        _store.ClearCache();
        // Die Seiten halten ihre Listen im Speicher - ohne diesen Aufruf waere
        // erst nach einem Neustart etwas zu sehen.
        _cacheCleared();
        Status = Loc.T("settings.data.cleared");
    }
}
