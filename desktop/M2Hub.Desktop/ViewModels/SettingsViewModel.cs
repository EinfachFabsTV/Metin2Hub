using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Ein Eintrag der Serverauswahl fuer die Kopfzeile.
public sealed record HeaderServerOption(string Key, string Label);

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
    private readonly Action _headerChanged;
    private readonly Action _cacheCleared;
    private readonly Func<UpdateService.UpdateInfo, Task> _showUpdate;

    private LanguageOption _language;
    private HeaderServerOption _headerServer;
    private bool _checkUpdates;
    private string? _status;
    private bool _busy;

    public SettingsViewModel(
        LocalStore store,
        IDialogService dialogs,
        UpdateService updates,
        Action headerChanged,
        Action cacheCleared,
        Func<UpdateService.UpdateInfo, Task> showUpdate)
    {
        _store = store;
        _dialogs = dialogs;
        _updates = updates;
        _headerChanged = headerChanged;
        _cacheCleared = cacheCleared;
        _showUpdate = showUpdate;

        _checkUpdates = store.Settings.CheckUpdates;
        _language = LanguageOptions.FirstOrDefault(o => o.Code == store.Settings.Language)
                    ?? LanguageOptions[0];
        HeaderServers = BuildHeaderServers(store);
        Servers = BuildServerRows(store);
        _headerServer = HeaderServers.FirstOrDefault(o => o.Key == store.Settings.HeaderServer)
                        ?? HeaderServers[0];

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

    public string Version => Loc.T("settings.version", UpdateService.CurrentVersion);
    public string DataFolder => LocalStore.Directory;

    public bool Busy { get => _busy; private set { if (Set(ref _busy, value)) Raise(nameof(NotBusy)); } }
    public bool NotBusy => !_busy;

    public string? Status { get => _status; private set { if (Set(ref _status, value)) Raise(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrWhiteSpace(_status);

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
