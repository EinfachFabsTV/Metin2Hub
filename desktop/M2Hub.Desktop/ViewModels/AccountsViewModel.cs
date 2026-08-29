using System.Collections.ObjectModel;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Accounts, Charaktere, Gilden und die Medaillen-Schnellwahl.
/// Alles liegt lokal im Nutzerprofil - kein Konto, keine Anmeldung, kein Server.
///
/// Aufbau nach der Excel-Vorlage: Kennzahlen oben, darunter ein Raster aus
/// Account-Kacheln. Je Kachel eine Mini-Tabelle mit Charakter, Medaillen und
/// Level; der Accountname traegt die Farbe seiner Client-Sprache, unten steht
/// die Kurzkennung. Die Spaltenzahl richtet sich nach der Fensterbreite.
public sealed class AccountsViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly IDialogService _dialogs;

    private readonly List<AccountItemViewModel> _allAccounts = new();

    private string? _error;
    private string? _status;
    private string _search = "";
    private string _sortMode = "eigene";
    private LanguageItemViewModel? _filterLanguage;
    private ServerOption? _filterServer;
    private RunOption? _filterRun;
    private bool _onlyWithCoins;
    private bool _loaded;
    private bool _showSettings;

    public AccountsViewModel(LocalStore store, IDialogService dialogs)
    {
        _store = store;
        _dialogs = dialogs;

        AddAccountCommand = new AsyncRelayCommand(_ => AddAccountAsync());
        EditAccountCommand = new AsyncRelayCommand(p => EditAccountAsync(p as AccountItemViewModel));
        DeleteAccountCommand = new AsyncRelayCommand(p => DeleteAccountAsync(p as AccountItemViewModel));
        AddCharacterCommand = new AsyncRelayCommand(p => AddCharacterAsync(p as AccountItemViewModel));
        EditCharacterCommand = new AsyncRelayCommand(p => EditCharacterAsync(p as CharacterItemViewModel));
        DeleteCharacterCommand = new AsyncRelayCommand(p => DeleteCharacterAsync(p as CharacterItemViewModel));

        MoveUpCommand = new RelayCommand(p => Move(p as AccountItemViewModel, -1));
        MoveDownCommand = new RelayCommand(p => Move(p as AccountItemViewModel, +1));

        AddGuildCommand = new RelayCommand(_ => AddGuild());
        SaveGuildCommand = new RelayCommand(p => SaveGuild(p as GuildItemViewModel));
        DeleteGuildCommand = new AsyncRelayCommand(p => DeleteGuildAsync(p as GuildItemViewModel));

        AddLanguageCommand = new RelayCommand(_ => AddLanguage());
        EditMedalsCommand = new RelayCommand(p => { if (p is CharacterItemViewModel c) c.EditingMedals = true; });
        SetMedalsCommand = new RelayCommand(p => SetMedals(p as CharacterItemViewModel));
        EditLevelCommand = new RelayCommand(p => { if (p is CharacterItemViewModel c) c.EditingLevel = true; });
        SetLevelCommand = new RelayCommand(p => SetLevel(p as CharacterItemViewModel));
        CancelLevelCommand = new RelayCommand(p => CancelLevel(p as CharacterItemViewModel));
        CancelMedalsCommand = new RelayCommand(p => CancelMedals(p as CharacterItemViewModel));
        SaveLanguageCommand = new RelayCommand(p => SaveLanguage(p as LanguageItemViewModel));
        DeleteLanguageCommand = new AsyncRelayCommand(p => DeleteLanguageAsync(p as LanguageItemViewModel));

        BulkMedalsCommand = new AsyncRelayCommand(_ => BulkMedalsAsync());
        AddPresetRowCommand = new RelayCommand(_ => PresetRows.Add(new PresetEditViewModel("+1", 1)));
        RemovePresetRowCommand = new RelayCommand(p => { if (p is PresetEditViewModel row) PresetRows.Remove(row); });
        SavePresetsCommand = new RelayCommand(_ => SavePresets());
        ToggleSettingsCommand = new RelayCommand(_ => ShowSettings = !ShowSettings);
    }

    /* ---------- Zustand ---------- */

    public string? Error { get => _error; private set { if (Set(ref _error, value)) Raise(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(_error);

    public string? Status { get => _status; private set { if (Set(ref _status, value)) Raise(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrWhiteSpace(_status);

    /// Die Kacheln in der aktuellen Sortierung und Filterung.
    public ObservableCollection<AccountItemViewModel> Accounts { get; } = new();

    public ObservableCollection<GuildItemViewModel> Guilds { get; } = new();
    public ObservableCollection<LanguageItemViewModel> Languages { get; } = new();
    public ObservableCollection<PresetEditViewModel> PresetRows { get; } = new();

    /// Auswahllisten mit fuehrendem Platzhalter (Id 0).
    public ObservableCollection<GuildItemViewModel> GuildChoices { get; } = new();
    public ObservableCollection<GuildItemViewModel> FilterGuilds { get; } = new();
    public ObservableCollection<LanguageItemViewModel> LanguageChoices { get; } = new();
    public ObservableCollection<LanguageItemViewModel> FilterLanguages { get; } = new();

    /// Server zur Zuordnung und zum Filtern. Beide Listen tragen vorn einen
    /// Platzhalter ohne Schluessel.
    public ObservableCollection<ServerOption> ServerChoices { get; } = new();
    public ObservableCollection<ServerOption> FilterServers { get; } = new();

    public static readonly GuildItemViewModel NoGuild =
        new(new GuildDto { Id = 0, Name = Loc.T("accounts.noGuild"), Level = 0 });
    public static readonly GuildItemViewModel AllGuilds =
        new(new GuildDto { Id = 0, Name = Loc.T("accounts.filter.allGuilds"), Level = 0 });
    public static readonly LanguageItemViewModel NoLanguage =
        new(new LanguageDto { Id = 0, Name = Loc.T("accounts.noGuild"), Color = "#9CA3AF" });
    public static readonly LanguageItemViewModel AllLanguages =
        new(new LanguageDto { Id = 0, Name = Loc.T("accounts.filter.allLanguages"), Color = "#9CA3AF" });

    public static readonly ServerOption NoServer = new("", Loc.T("accounts.noServer"));
    public static readonly ServerOption AllServers = new("", Loc.T("accounts.filter.allServers"));

    private static int? RealId(GuildItemViewModel? guild) => guild is null || guild.Id == 0 ? null : guild.Id;
    private static int? RealId(LanguageItemViewModel? language) => language is null || language.Id == 0 ? null : language.Id;

    /// Sortierarten. Gespeichert wird der Schluessel, angezeigt der Text in
    /// der eingestellten Sprache.
    public ObservableCollection<SortOption> SortModes { get; } = new()
    {
        new("eigene", Loc.T("accounts.sort.own")),
        new("name", Loc.T("accounts.sort.name")),
        new("medals", Loc.T("accounts.sort.medals")),
        new("level", Loc.T("accounts.sort.level")),
        new("language", Loc.T("accounts.sort.language")),
        new("coins", Loc.T("accounts.sort.coins")),
    };

    /// Sortierung der Kacheln. „eigene" heisst: die selbst gelegte Reihenfolge.
    public SortOption SortMode
    {
        get => SortModes.FirstOrDefault(o => o.Key == _sortMode) ?? SortModes[0];
        set
        {
            var key = value?.Key ?? "eigene";
            if (!Set(ref _sortMode, key)) return;
            _store.Accounts.SortMode = key;
            _store.SaveAccounts();
            Raise(nameof(CanReorder));
            ApplyFilter();
        }
    }

    /// Verschieben ergibt nur in der eigenen Reihenfolge Sinn.
    public bool CanReorder => _sortMode == "eigene";

    public string Search
    {
        get => _search;
        set { if (Set(ref _search, value)) ApplyFilter(); }
    }

    public LanguageItemViewModel? FilterLanguage
    {
        get => _filterLanguage;
        set { if (Set(ref _filterLanguage, value)) ApplyFilter(); }
    }

    /// Wer auf mehreren Servern spielt, sieht hier nur die Accounts eines.
    public ServerOption? FilterServer
    {
        get => _filterServer;
        set { if (Set(ref _filterServer, value)) ApplyFilter(); }
    }

    /// Ohne bekannten Server waere die Auswahl leer - dann bleibt sie weg.
    public bool HasServers => FilterServers.Count > 1;

    /// Filter nach Lauf: zeigt die Accounts, auf denen ein Char dafuer steht.
    public ObservableCollection<RunOption> FilterRuns { get; } = new()
    {
        new("", Loc.T("accounts.filter.allRuns")),
        new("meley", Loc.T("char.form.meley")),
        new("balathor", Loc.T("char.form.balathor")),
        new("serpent", Loc.T("char.form.serpent")),
        new("donate", Loc.T("char.form.donate")),
        new("grotte", Loc.T("char.form.grotte")),
    };

    public RunOption? FilterRun
    {
        get => _filterRun;
        set { if (Set(ref _filterRun, value)) ApplyFilter(); }
    }

    /// Nur Accounts, auf denen Drachenmuenzen liegen.
    public bool OnlyWithCoins
    {
        get => _onlyWithCoins;
        set { if (Set(ref _onlyWithCoins, value)) ApplyFilter(); }
    }

    public bool ShowSettings { get => _showSettings; private set => Set(ref _showSettings, value); }

    /// Escape auf der Seite: erst das Verwaltungsfeld schliessen, dann die
    /// Suche leeren. Gibt zurueck, ob es etwas zu tun gab.
    public bool Cancel()
    {
        if (ShowSettings) { ShowSettings = false; return true; }
        if (_search.Length > 0) { Search = ""; return true; }
        return false;
    }

    private string _newGuildName = "";
    private int _newGuildLevel = 20;
    public string NewGuildName { get => _newGuildName; set => Set(ref _newGuildName, value); }
    public int NewGuildLevel { get => _newGuildLevel; set => Set(ref _newGuildLevel, Math.Clamp(value, 1, 40)); }

    private string _newLanguageName = "";
    public string NewLanguageName { get => _newLanguageName; set => Set(ref _newLanguageName, value); }

    /// Farben fuer neue Client-Sprachen. Vergeben wird die erste, die noch
    /// keine Sprache traegt - erst wenn alle vergeben sind, faengt es von vorn
    /// an. Von Hand aendern laesst sie sich in der Liste weiterhin.
    private static readonly string[] LanguagePalette =
    [
        "#EF4444", "#10B981", "#F59E0B", "#3B82F6", "#A855F7",
        "#EC4899", "#14B8A6", "#F97316", "#6366F1", "#84CC16",
    ];

    private string NextLanguageColor()
    {
        var used = _store.Accounts.Languages
            .Select(l => l.Color.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return LanguagePalette.FirstOrDefault(c => !used.Contains(c))
               ?? LanguagePalette[_store.Accounts.Languages.Count % LanguagePalette.Length];
    }

    private int _bulkDelta = 12;
    private GuildItemViewModel? _bulkGuild;
    private string _bulkScope = "all";
    public int BulkDelta { get => _bulkDelta; set => Set(ref _bulkDelta, value); }
    public GuildItemViewModel? BulkGuild { get => _bulkGuild; set => Set(ref _bulkGuild, value); }

    /// Wer bei der Sammelvergabe bedacht wird: wirklich alle Charaktere oder
    /// nur die Spenden-Chars. Die Gilde daneben schraenkt zusaetzlich ein.
    public ObservableCollection<RunOption> BulkScopes { get; } = new()
    {
        new("all", Loc.T("accounts.bulk.scope.all")),
        new("donate", Loc.T("accounts.bulk.scope.donate")),
    };

    public RunOption BulkScope
    {
        get => BulkScopes.FirstOrDefault(o => o.Key == _bulkScope) ?? BulkScopes[0];
        set => Set(ref _bulkScope, value?.Key ?? "all");
    }

    /* ---------- Kennzahlen (wie in der Vorlage) ---------- */

    public int TotalMedals => _allAccounts.Sum(a => a.Medals);
    public int TotalAccounts => _allAccounts.Count;
    public int TotalCharacters => _allAccounts.Sum(a => a.CharacterCount);
    public int AverageMedals => TotalCharacters == 0 ? 0 : TotalMedals / TotalCharacters;
    public int TotalDragonCoins => _allAccounts.Sum(a => a.DragonCoins);

    /// „12 von 17" - die Tombola haengt an der Bio je Account.
    public string BioProgress => $"{_allAccounts.Count(a => a.BioDone)} von {TotalAccounts}";

    private void RaiseTotals()
    {
        Raise(nameof(TotalMedals));
        Raise(nameof(TotalAccounts));
        Raise(nameof(TotalCharacters));
        Raise(nameof(AverageMedals));
        Raise(nameof(TotalDragonCoins));
        Raise(nameof(BioProgress));
    }

    /* ---------- Befehle ---------- */

    public AsyncRelayCommand AddAccountCommand { get; }
    public AsyncRelayCommand EditAccountCommand { get; }
    public AsyncRelayCommand DeleteAccountCommand { get; }
    public AsyncRelayCommand AddCharacterCommand { get; }
    public AsyncRelayCommand EditCharacterCommand { get; }
    public AsyncRelayCommand DeleteCharacterCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand AddGuildCommand { get; }
    public RelayCommand SaveGuildCommand { get; }
    public AsyncRelayCommand DeleteGuildCommand { get; }
    public RelayCommand AddLanguageCommand { get; }

    /// Doppelklick auf die Medaillen einer Zeile bzw. das Uebernehmen danach.
    public RelayCommand EditMedalsCommand { get; }
    public RelayCommand SetMedalsCommand { get; }

    /// Dasselbe fuer das Level.
    public RelayCommand EditLevelCommand { get; }
    public RelayCommand SetLevelCommand { get; }

    /// Escape: die Eingabe verwerfen und den gespeicherten Wert zurueckholen.
    public RelayCommand CancelLevelCommand { get; }
    public RelayCommand CancelMedalsCommand { get; }
    public RelayCommand SaveLanguageCommand { get; }
    public AsyncRelayCommand DeleteLanguageCommand { get; }
    public AsyncRelayCommand BulkMedalsCommand { get; }
    public RelayCommand AddPresetRowCommand { get; }
    public RelayCommand RemovePresetRowCommand { get; }
    public RelayCommand SavePresetsCommand { get; }
    public RelayCommand ToggleSettingsCommand { get; }

    /* ---------- Laden ---------- */

    public void EnsureLoaded()
    {
        if (_loaded) return;
        Load();
        _loaded = true;
    }

    /// Nach einem Abruf oder einer geaenderten Sichtbarkeit: die Serverlisten
    /// neu aufbauen. Vor dem ersten Laden gibt es noch nichts zu tun.
    public void RefreshServers()
    {
        if (!_loaded) return;
        RebuildChoices();
        ApplyFilter();
    }

    private void Load()
    {
        var data = _store.Accounts;

        // Zuordnungen auf einen gemeinsamen Kalender sind keine Serverwahl -
        // sie werden einmalig geloest (siehe ServerCatalog).
        if (ServerCatalog.DropGroupAssignments(_store)) _store.SaveAccounts();

        Languages.Clear();
        foreach (var l in data.Languages.OrderBy(l => l.Sort).ThenBy(l => l.Id))
            Languages.Add(new LanguageItemViewModel(l));

        Guilds.Clear();
        foreach (var g in data.Guilds.OrderBy(g => g.Sort).ThenBy(g => g.Id))
            Guilds.Add(new GuildItemViewModel(g));

        RebuildChoices();

        _allAccounts.Clear();
        var guilds = GuildChoices.ToList();
        var languages = LanguageChoices.ToList();
        foreach (var a in data.Accounts.OrderBy(a => a.Sort).ThenBy(a => a.Id))
            _allAccounts.Add(new AccountItemViewModel(a, guilds, languages));

        PresetRows.Clear();
        foreach (var p in data.Presets) PresetRows.Add(new PresetEditViewModel(p.Label, p.Value));

        _sortMode = SortModes.Any(o => o.Key == data.SortMode) ? data.SortMode : "eigene";
        Raise(nameof(SortMode));
        Raise(nameof(CanReorder));

        RebuildPresetButtons();
        RecountGuilds();
        ApplyFilter();
    }

    private void RebuildChoices()
    {
        GuildChoices.Clear();
        GuildChoices.Add(NoGuild);
        FilterGuilds.Clear();
        FilterGuilds.Add(AllGuilds);
        foreach (var g in Guilds)
        {
            GuildChoices.Add(g);
            FilterGuilds.Add(g);
        }

        LanguageChoices.Clear();
        LanguageChoices.Add(NoLanguage);
        FilterLanguages.Clear();
        FilterLanguages.Add(AllLanguages);
        foreach (var l in Languages)
        {
            LanguageChoices.Add(l);
            FilterLanguages.Add(l);
        }

        // Die Server kommen aus den geladenen Kalendern; ausgeblendete bleiben
        // draussen, ausser ein Account haengt noch daran.
        ServerChoices.Clear();
        ServerChoices.Add(NoServer);
        FilterServers.Clear();
        FilterServers.Add(AllServers);
        var assigned = _store.Accounts.Accounts
            .Select(a => a.ServerKey)
            .Where(k => k.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var server in ServerCatalog.GameServers(_store))
        {
            if (ServerCatalog.IsHidden(_store, server.Key) && !assigned.Contains(server.Key)) continue;
            ServerChoices.Add(server);
            FilterServers.Add(new ServerOption(server.Key, server.Label));
        }

        BulkGuild ??= AllGuilds;
        FilterLanguage ??= AllLanguages;
        FilterServer = FilterServers.FirstOrDefault(o => o.Key == (_filterServer?.Key ?? "")) ?? AllServers;
        FilterRun ??= FilterRuns[0];
        Raise(nameof(HasServers));
    }

    /// Nach einem Sprachwechsel: die festen Beschriftungen neu setzen.
    public void RelabelAfterLanguageChange()
    {
        SortModes.Clear();
        SortModes.Add(new SortOption("eigene", Loc.T("accounts.sort.own")));
        SortModes.Add(new SortOption("name", Loc.T("accounts.sort.name")));
        SortModes.Add(new SortOption("medals", Loc.T("accounts.sort.medals")));
        SortModes.Add(new SortOption("level", Loc.T("accounts.sort.level")));
        SortModes.Add(new SortOption("language", Loc.T("accounts.sort.language")));
        SortModes.Add(new SortOption("coins", Loc.T("accounts.sort.coins")));
        Raise(nameof(SortMode));

        // Auch die Laeufe tragen ihre Beschriftung als feste Zeichenkette.
        var run = _filterRun?.Key ?? "";
        FilterRuns.Clear();
        FilterRuns.Add(new RunOption("", Loc.T("accounts.filter.allRuns")));
        FilterRuns.Add(new RunOption("meley", Loc.T("char.form.meley")));
        FilterRuns.Add(new RunOption("balathor", Loc.T("char.form.balathor")));
        FilterRuns.Add(new RunOption("serpent", Loc.T("char.form.serpent")));
        FilterRuns.Add(new RunOption("donate", Loc.T("char.form.donate")));
        FilterRuns.Add(new RunOption("grotte", Loc.T("char.form.grotte")));
        FilterRun = FilterRuns.FirstOrDefault(o => o.Key == run) ?? FilterRuns[0];

        var scope = _bulkScope;
        BulkScopes.Clear();
        BulkScopes.Add(new RunOption("all", Loc.T("accounts.bulk.scope.all")));
        BulkScopes.Add(new RunOption("donate", Loc.T("accounts.bulk.scope.donate")));
        _bulkScope = scope;
        Raise(nameof(BulkScope));

        NoGuild.Name = Loc.T("accounts.noGuild");
        AllGuilds.Name = Loc.T("accounts.filter.allGuilds");
        NoLanguage.Name = Loc.T("accounts.noGuild");
        AllLanguages.Name = Loc.T("accounts.filter.allLanguages");
        NoServer.Label = Loc.T("accounts.noServer");
        AllServers.Label = Loc.T("accounts.filter.allServers");
    }

    /* ---------- Filter und Sortierung ---------- */

    private void ApplyFilter()
    {
        var term = _search.Trim();
        var languageId = RealId(FilterLanguage);
        var serverKey = _filterServer?.Key ?? "";

        IEnumerable<AccountItemViewModel> query = _allAccounts;

        if (languageId is int lid)
            query = query.Where(a => a.Language?.Id == lid);

        if (serverKey.Length > 0)
            query = query.Where(a => a.ServerKey == serverKey);

        // Lauf: es genuegt ein Char des Accounts, der dafuer steht.
        if ((_filterRun?.Key ?? "") is { Length: > 0 } run)
            query = query.Where(a => a.Characters.Any(c => HasRun(c, run)));

        if (_onlyWithCoins)
            query = query.Where(a => a.DragonCoins > 0);

        if (term.Length > 0)
            query = query.Where(a => Matches(a, term));

        query = _sortMode switch
        {
            "name" => query.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase),
            "medals" => query.OrderByDescending(a => a.Medals).ThenBy(a => a.Name),
            "level" => query.OrderByDescending(a => a.Characters.Count == 0 ? 0 : a.Characters.Max(c => c.Level))
                            .ThenBy(a => a.Name),
            "language" => query.OrderBy(a => a.LanguageName).ThenBy(a => a.Name),
            "coins" => query.OrderByDescending(a => a.DragonCoins).ThenBy(a => a.Name),
            // „eigene": die gespeicherte Reihenfolge, die _allAccounts ohnehin haelt
            _ => query,
        };

        Accounts.Clear();
        foreach (var a in query) Accounts.Add(a);

        RaiseTotals();
    }

    private static bool HasRun(CharacterItemViewModel character, string run) => run switch
    {
        "meley" => character.IsMeley,
        "balathor" => character.IsBalathor,
        "serpent" => character.IsSerpent,
        "donate" => character.IsDonate,
        "grotte" => character.IsGrotte,
        _ => true,
    };

    private static bool Matches(AccountItemViewModel account, string term)
    {
        var cmp = StringComparison.CurrentCultureIgnoreCase;
        if (account.Name.Contains(term, cmp) || account.Note.Contains(term, cmp)) return true;
        return account.Characters.Any(c => c.Name.Contains(term, cmp));
    }

    private void RecountGuilds()
    {
        foreach (var g in Guilds) g.Medals = 0;
        foreach (var a in _allAccounts)
        {
            a.Recount();
            foreach (var c in a.Characters)
                if (c.Guild is not null && c.Guild.Id != 0) c.Guild.Medals += c.Medals;
        }
        RaiseTotals();
    }

    private void RebuildPresetButtons()
    {
        foreach (var account in _allAccounts)
            foreach (var character in account.Characters)
                RebuildPresetButtons(character);
    }

    private void RebuildPresetButtons(CharacterItemViewModel character)
    {
        character.Presets.Clear();
        foreach (var p in _store.Accounts.Presets)
            character.Presets.Add(new MedalPresetViewModel(p.Label, p.Value, character, QuickMedals));
    }

    /* ---------- Zugriff auf die gespeicherten Rohdaten ---------- */

    private AccountDto? Dto(AccountItemViewModel account) =>
        _store.Accounts.Accounts.FirstOrDefault(a => a.Id == account.Id);

    private CharacterDto? Dto(CharacterItemViewModel character) =>
        _store.Accounts.Accounts.SelectMany(a => a.Characters).FirstOrDefault(c => c.Id == character.Id);

    private GuildDto? Dto(GuildItemViewModel guild) =>
        _store.Accounts.Guilds.FirstOrDefault(g => g.Id == guild.Id);

    private LanguageDto? Dto(LanguageItemViewModel language) =>
        _store.Accounts.Languages.FirstOrDefault(l => l.Id == language.Id);

    private void Save(string? message = null)
    {
        _store.SaveAccounts();
        Status = message;
        Error = null;
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    /* ---------- Accounts ---------- */

    /// Traegt einen selbst genannten Server ein, sofern die Maske einen nennt,
    /// und gibt zurueck, was zu speichern ist. Ein Name, den es schon gibt,
    /// waehlt den bestehenden Eintrag statt einen zweiten anzulegen.
    private ServerOption? ChosenServer(AccountEditViewModel model)
    {
        var name = model.NewServer.Trim();
        if (name.Length == 0) return model.Server?.IsRealServer == true ? model.Server : null;

        var key = ServerCatalog.CustomKey(name);
        var known = ServerChoices.FirstOrDefault(o => o.Key == key);
        if (known is not null) return known;

        _store.Accounts.CustomServers.Add(Cut(name, 60));
        return new ServerOption(key, Cut(name, 60));
    }

    private async Task AddAccountAsync()
    {
        var model = new AccountEditViewModel(Loc.T("accounts.newTitle"), null, LanguageChoices, ServerChoices);
        if (!await _dialogs.EditAccountAsync(model)) return;

        var chosenServer = ChosenServer(model);

        var dto = new AccountDto
        {
            Id = _store.Accounts.TakeId(),
            Name = Cut(model.Name.Trim(), 60),
            Note = Cut(model.Note.Trim(), 200),
            DragonCoins = model.DragonCoins,
            LanguageId = RealId(model.Language),
            ServerKey = chosenServer?.Key ?? "",
            ServerLabel = chosenServer?.Label ?? "",
            Sort = _store.Accounts.Accounts.Count,
        };
        _store.Accounts.Accounts.Add(dto);

        _allAccounts.Add(new AccountItemViewModel(dto, GuildChoices.ToList(), LanguageChoices.ToList()));
        RebuildChoices();
        ApplyFilter();
        Save($"Account „{dto.Name}“ angelegt.");
    }

    private async Task EditAccountAsync(AccountItemViewModel? account)
    {
        if (account is null || Dto(account) is not { } dto) return;

        var model = new AccountEditViewModel(Loc.T("accounts.edit"), account, LanguageChoices, ServerChoices);
        if (!await _dialogs.EditAccountAsync(model)) return;

        dto.Name = Cut(model.Name.Trim(), 60);
        dto.Note = Cut(model.Note.Trim(), 200);
        dto.DragonCoins = model.DragonCoins;
        dto.LanguageId = RealId(model.Language);
        var chosenServer = ChosenServer(model);
        dto.ServerKey = chosenServer?.Key ?? "";
        dto.ServerLabel = chosenServer?.Label ?? "";

        account.Name = dto.Name;
        account.Note = dto.Note;
        account.DragonCoins = dto.DragonCoins;
        account.Language = model.Language;
        account.Server = chosenServer;

        RebuildChoices();
        ApplyFilter();
        Save("Account gespeichert.");
    }

    private async Task DeleteAccountAsync(AccountItemViewModel? account)
    {
        if (account is null) return;
        var ok = await _dialogs.ConfirmAsync(
            "Account löschen",
            $"„{account.Name}“ und alle {account.CharacterCount} zugehörigen Charaktere werden gelöscht. Das lässt sich nicht rückgängig machen.");
        if (!ok) return;

        _store.Accounts.Accounts.RemoveAll(a => a.Id == account.Id);
        _allAccounts.Remove(account);
        Renumber();
        RecountGuilds();
        ApplyFilter();
        Save("Account gelöscht.");
    }

    /// Nach jeder Umsortierung die gespeicherte Reihenfolge nachziehen.
    private void Renumber()
    {
        for (var i = 0; i < _allAccounts.Count; i++)
            if (Dto(_allAccounts[i]) is { } dto) dto.Sort = i;
    }

    /// Verschiebt eine Kachel vor bzw. hinter eine andere - das ist, was beim
    /// Ziehen passiert. Die Pfeile bleiben daneben bestehen: mit der Tastatur
    /// oder auf kleinen Bildschirmen ist Ziehen muehsam.
    ///
    /// Gearbeitet wird auf der vollstaendigen Liste, nicht auf der gefilterten:
    /// sonst spraenge eine Kachel an eine ganz andere Stelle, sobald ein Filter
    /// gesetzt ist.
    public void MoveBefore(AccountItemViewModel? source, AccountItemViewModel? target)
    {
        if (source is null || target is null || ReferenceEquals(source, target)) return;
        if (!CanReorder) return;

        var from = _allAccounts.IndexOf(source);
        var to = _allAccounts.IndexOf(target);
        if (from < 0 || to < 0) return;

        _allAccounts.RemoveAt(from);
        _allAccounts.Insert(to, source);
        Renumber();
        ApplyFilter();
        Save();
    }

    private void Move(AccountItemViewModel? account, int direction)
    {
        if (account is null || !CanReorder) return;

        var index = _allAccounts.IndexOf(account);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= _allAccounts.Count) return;

        _allAccounts.RemoveAt(index);
        _allAccounts.Insert(target, account);
        Renumber();
        ApplyFilter();
        Save();
    }

    /* ---------- Charaktere ---------- */

    private async Task AddCharacterAsync(AccountItemViewModel? account)
    {
        if (account is null || Dto(account) is not { } accountDto) return;

        var model = new CharacterEditViewModel(Loc.T("accounts.addChar"), null, GuildChoices);
        if (!await _dialogs.EditCharacterAsync(model)) return;

        var dto = new CharacterDto
        {
            Id = _store.Accounts.TakeId(),
            AccountId = account.Id,
            Name = Cut(model.Name.Trim(), 60),
            Level = model.Level,
            Medals = model.Medals,
            GuildId = RealId(model.Guild),
            IsMeley = model.IsMeley,
            IsGrotte = model.IsGrotte,
            IsBalathor = model.IsBalathor,
            IsSerpent = model.IsSerpent,
            IsDonate = model.IsDonate,
            IsBio = model.IsBio,
            BioDone = model.BioDone,
            Sort = accountDto.Characters.Count,
        };
        accountDto.Characters.Add(dto);

        var item = new CharacterItemViewModel(dto, GuildChoices.ToList());
        RebuildPresetButtons(item);
        account.Characters.Add(item);
        account.Recount();

        RecountGuilds();
        Save($"„{dto.Name}“ angelegt.");
    }

    private async Task EditCharacterAsync(CharacterItemViewModel? character)
    {
        if (character is null || Dto(character) is not { } dto) return;

        var model = new CharacterEditViewModel(character.Name, character, GuildChoices);
        if (!await _dialogs.EditCharacterAsync(model)) return;

        dto.Name = Cut(model.Name.Trim(), 60);
        dto.Level = model.Level;
        dto.Medals = model.Medals;
        dto.GuildId = RealId(model.Guild);
        dto.IsMeley = model.IsMeley;
        dto.IsGrotte = model.IsGrotte;
        dto.IsBalathor = model.IsBalathor;
        dto.IsSerpent = model.IsSerpent;
        dto.IsDonate = model.IsDonate;
        dto.IsBio = model.IsBio;
        dto.BioDone = model.BioDone;

        character.Name = dto.Name;
        character.Level = dto.Level;
        character.Medals = dto.Medals;
        character.Guild = model.Guild;
        character.IsMeley = dto.IsMeley;
        character.IsGrotte = dto.IsGrotte;
        character.IsBalathor = dto.IsBalathor;
        character.IsSerpent = dto.IsSerpent;
        character.IsDonate = dto.IsDonate;
        character.IsBio = dto.IsBio;
        character.BioDone = dto.BioDone;
        character.Refresh();

        Owner(character)?.Recount();
        RecountGuilds();
        Save($"„{dto.Name}“ gespeichert.");
    }

    private AccountItemViewModel? Owner(CharacterItemViewModel character) =>
        _allAccounts.FirstOrDefault(a => a.Characters.Contains(character));

    private async Task DeleteCharacterAsync(CharacterItemViewModel? character)
    {
        if (character is null) return;
        var account = Owner(character);
        if (account is null) return;

        var ok = await _dialogs.ConfirmAsync("Charakter löschen", $"„{character.Name}“ wirklich löschen?");
        if (!ok) return;

        if (Dto(account) is { } accountDto) accountDto.Characters.RemoveAll(c => c.Id == character.Id);
        account.Characters.Remove(character);
        account.Recount();
        RecountGuilds();
        Save("Charakter gelöscht.");
    }

    /// Schnellwahl: aufaddieren, nie unter 0.
    /// Uebernimmt die von Hand eingetippten Medaillen einer Zeile.
    private void SetMedals(CharacterItemViewModel? character)
    {
        if (character is null) return;
        character.EditingMedals = false;

        if (Dto(character) is not { } dto || dto.Medals == character.Medals) return;

        dto.Medals = Math.Max(0, character.Medals);
        character.SetMedals(dto.Medals);
        Owner(character)?.Recount();
        RecountGuilds();
        RaiseTotals();
        Save();
    }

    private void SetLevel(CharacterItemViewModel? character)
    {
        if (character is null) return;
        character.EditingLevel = false;

        if (Dto(character) is not { } dto || dto.Level == character.Level) return;

        dto.Level = Math.Clamp(character.Level, 1, 120);
        character.SetLevel(dto.Level);
        ApplyFilter();
        Save();
    }

    /// Abbrechen heisst: den Stand aus der Ablage wieder anzeigen.
    private void CancelLevel(CharacterItemViewModel? character)
    {
        if (character is null) return;
        character.EditingLevel = false;
        if (Dto(character) is { } dto) character.SetLevel(dto.Level);
    }

    private void CancelMedals(CharacterItemViewModel? character)
    {
        if (character is null) return;
        character.EditingMedals = false;
        if (Dto(character) is { } dto) character.SetMedals(dto.Medals);
    }

    private Task QuickMedals(CharacterItemViewModel character, int delta)
    {
        if (Dto(character) is not { } dto) return Task.CompletedTask;

        dto.Medals = Math.Max(0, dto.Medals + delta);
        character.SetMedals(dto.Medals);
        Owner(character)?.Recount();
        RecountGuilds();
        Save();
        return Task.CompletedTask;
    }

    private async Task BulkMedalsAsync()
    {
        if (BulkDelta == 0) { Error = Loc.T("accounts.bulk.needAmount"); return; }

        var guildId = RealId(BulkGuild);
        var onlyDonate = _bulkScope == "donate";

        // Der Satz nennt beides: die Rolle und, falls gesetzt, die Gilde -
        // sonst weiss man vor dem Bestaetigen nicht, wen es trifft.
        var scope = (onlyDonate, guildId is null) switch
        {
            (true, false) => Loc.T("accounts.bulk.scope.donateGuild", BulkGuild!.Name),
            (true, true) => Loc.T("accounts.bulk.scope.donateChars"),
            (false, false) => Loc.T("accounts.bulk.scope.allGuild", BulkGuild!.Name),
            _ => Loc.T("accounts.bulk.scope.allChars"),
        };

        var ok = await _dialogs.ConfirmAsync(
            Loc.T("accounts.bulk.title"),
            Loc.T("accounts.bulk.confirm", $"{(BulkDelta > 0 ? "+" : "")}{BulkDelta}", scope),
            Loc.T("common.apply"));
        if (!ok) return;

        var changed = 0;
        foreach (var dto in _store.Accounts.Accounts.SelectMany(a => a.Characters))
        {
            if (guildId is int gid && dto.GuildId != gid) continue;
            if (onlyDonate && !dto.IsDonate) continue;
            dto.Medals = Math.Max(0, dto.Medals + BulkDelta);
            changed++;
        }

        Load();
        Save(Loc.T("accounts.bulk.done", changed));
    }

    /* ---------- Gilden ---------- */

    private void AddGuild()
    {
        var name = NewGuildName.Trim();
        if (name.Length == 0) { Error = Loc.T("form.needName"); return; }

        var dto = new GuildDto
        {
            Id = _store.Accounts.TakeId(),
            Name = Cut(name, 120),
            Level = Math.Clamp(NewGuildLevel, 1, 40),
            Sort = _store.Accounts.Guilds.Count,
        };
        _store.Accounts.Guilds.Add(dto);

        Guilds.Add(new GuildItemViewModel(dto));
        RebuildChoices();
        NewGuildName = "";
        Save($"Gilde „{dto.Name}“ angelegt.");
    }

    private void SaveGuild(GuildItemViewModel? guild)
    {
        if (guild is null || Dto(guild) is not { } dto) return;
        var name = guild.Name.Trim();
        if (name.Length == 0) { Error = Loc.T("form.needName"); return; }

        dto.Name = Cut(name, 120);
        dto.Level = Math.Clamp(guild.Level, 1, 40);
        Save($"Gilde „{dto.Name}“ gespeichert.");
    }

    private async Task DeleteGuildAsync(GuildItemViewModel? guild)
    {
        if (guild is null) return;
        var ok = await _dialogs.ConfirmAsync(
            "Gilde löschen",
            $"„{guild.Name}“ löschen? Die Charaktere bleiben erhalten und verlieren nur die Zuordnung.");
        if (!ok) return;

        _store.Accounts.Guilds.RemoveAll(g => g.Id == guild.Id);
        foreach (var c in _store.Accounts.Accounts.SelectMany(a => a.Characters))
            if (c.GuildId == guild.Id) c.GuildId = null;

        Load();
        Save("Gilde gelöscht.");
    }

    /* ---------- Client-Sprachen ---------- */

    private void AddLanguage()
    {
        var name = NewLanguageName.Trim();
        if (name.Length == 0) { Error = Loc.T("form.needName"); return; }

        var dto = new LanguageDto
        {
            Id = _store.Accounts.TakeId(),
            Name = Cut(name, 40),
            Color = NextLanguageColor(),
            Sort = _store.Accounts.Languages.Count,
        };
        _store.Accounts.Languages.Add(dto);

        Languages.Add(new LanguageItemViewModel(dto));
        RebuildChoices();
        NewLanguageName = "";
        Save($"Sprache „{dto.Name}“ angelegt.");
    }

    private void SaveLanguage(LanguageItemViewModel? language)
    {
        if (language is null || Dto(language) is not { } dto) return;
        var name = language.Name.Trim();
        if (name.Length == 0) { Error = Loc.T("form.needName"); return; }

        dto.Name = Cut(name, 40);
        dto.Color = language.Color.Trim();
        // Die Accountnamen tragen diese Farbe - sie muessen neu gezeichnet werden.
        foreach (var a in _allAccounts) a.Language = a.Language;
        Save($"Sprache „{dto.Name}“ gespeichert.");
    }

    private async Task DeleteLanguageAsync(LanguageItemViewModel? language)
    {
        if (language is null) return;
        var used = _store.Accounts.Accounts.Count(a => a.LanguageId == language.Id);
        var ok = await _dialogs.ConfirmAsync(
            "Sprache löschen",
            used == 0
                ? $"„{language.Name}“ löschen?"
                : $"„{language.Name}“ löschen? {used} Accounts verlieren dadurch ihre Sprache.");
        if (!ok) return;

        _store.Accounts.Languages.RemoveAll(l => l.Id == language.Id);
        foreach (var a in _store.Accounts.Accounts)
            if (a.LanguageId == language.Id) a.LanguageId = null;

        Load();
        Save("Sprache gelöscht.");
    }

    /* ---------- Schnellwahl ---------- */

    private void SavePresets()
    {
        var presets = PresetRows
            .Where(r => !string.IsNullOrWhiteSpace(r.Label) && r.Value != 0)
            .Take(8)
            .Select(r => new PresetDto { Label = Cut(r.Label.Trim(), 20), Value = r.Value })
            .ToList();

        _store.Accounts.Presets = presets;
        PresetRows.Clear();
        foreach (var p in presets) PresetRows.Add(new PresetEditViewModel(p.Label, p.Value));
        RebuildPresetButtons();
        Save("Schnellwahl gespeichert.");
    }
}
