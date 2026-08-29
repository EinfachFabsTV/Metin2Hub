using System.Windows.Input;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Grundlage aller Masken. Sie werden nicht als eigenes Fenster geoeffnet,
/// sondern ueber die Ansicht gelegt - so gibt es keine Fensterrahmen des
/// Betriebssystems und das Bild bleibt aus einem Guss.
public abstract class DialogViewModelBase : ViewModelBase
{
    private readonly TaskCompletionSource<bool> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private string? _error;

    protected DialogViewModelBase(string title)
    {
        Title = title;
        ConfirmCommand = new RelayCommand(_ => Confirm());
        CancelCommand = new RelayCommand(_ => Close(false));
    }

    public string Title { get; }
    public string ConfirmLabel { get; protected init; } = Loc.T("common.save");
    public string CancelLabel { get; protected init; } = Loc.T("common.cancel");

    /// Zerstoerende Aktionen bekommen einen roten Knopf.
    public bool IsDestructive { get; protected init; }

    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public Task<bool> Completion => _completion.Task;

    public string? Error
    {
        get => _error;
        protected set { if (Set(ref _error, value)) Raise(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(_error);

    /// Vor dem Schliessen pruefen; false haelt die Maske offen.
    protected virtual bool Validate() => true;

    private void Confirm()
    {
        if (!Validate()) return;
        Close(true);
    }

    public void Close(bool result) => _completion.TrySetResult(result);
}

/// Rueckfrage mit einer Meldung.
public sealed class ConfirmDialogViewModel : DialogViewModelBase
{
    public ConfirmDialogViewModel(string title, string message, string confirmLabel)
        : base(title)
    {
        Message = message;
        ConfirmLabel = confirmLabel;
        IsDestructive = true;
    }

    public string Message { get; }
}

/// Eingabemaske fuer einen Account.
public sealed class AccountEditViewModel : DialogViewModelBase
{
    private string _name;
    private string _note;
    private int _dragonCoins;
    private LanguageItemViewModel? _language;
    private ServerOption? _server;
    private string _newServer = "";

    public AccountEditViewModel(
        string title,
        AccountItemViewModel? account,
        IReadOnlyList<LanguageItemViewModel> languages,
        IReadOnlyList<ServerOption> servers)
        : base(title)
    {
        _name = account?.Name ?? "";
        _note = account?.Note ?? "";
        _dragonCoins = account?.DragonCoins ?? 0;

        Languages = new List<LanguageItemViewModel>(languages);
        _language = account?.Language ?? Languages.FirstOrDefault();

        Servers = new List<ServerOption>(servers);
        // Der Server des Accounts kann fehlen, wenn sein Kalender gerade nicht
        // vorliegt - dann steht er trotzdem zur Wahl, statt still zu verfallen.
        if (account?.Server is { } own && Servers.All(o => o.Key != own.Key)) Servers.Add(own);
        _server = Servers.FirstOrDefault(o => o.Key == (account?.ServerKey ?? ""))
                  ?? Servers.FirstOrDefault();
    }

    public IReadOnlyList<LanguageItemViewModel> Languages { get; }

    /// Auf welchem Server der Account spielt. Vorn steht „kein Server".
    public List<ServerOption> Servers { get; }
    public bool HasServers => Servers.Count > 1;

    public string Name { get => _name; set => Set(ref _name, value); }
    public string Note { get => _note; set => Set(ref _note, value); }
    public int DragonCoins { get => _dragonCoins; set => Set(ref _dragonCoins, Math.Max(0, value)); }
    public LanguageItemViewModel? Language { get => _language; set => Set(ref _language, value); }
    public ServerOption? Server { get => _server; set => Set(ref _server, value); }

    /// Ein Server, der nicht in der Liste steht. Steht hier etwas, gilt er -
    /// die Auswahl daneben wird dann uebergangen.
    public string NewServer
    {
        get => _newServer;
        set => Set(ref _newServer, value);
    }

    protected override bool Validate()
    {
        Error = string.IsNullOrWhiteSpace(Name) ? Loc.T("form.needName") : null;
        return !HasError;
    }
}

/// Eingabemaske fuer einen Charakter samt seiner Rollen.
public sealed class CharacterEditViewModel : DialogViewModelBase
{
    private string _name;
    private int _level;
    private int _medals;
    private GuildItemViewModel? _guild;
    private bool _isMeley;
    private bool _isGrotte;
    private bool _isBalathor;
    private bool _isSerpent;
    private bool _isDonate;
    private bool _isBio;
    private bool _bioDone;

    public CharacterEditViewModel(
        string title,
        CharacterItemViewModel? character,
        IReadOnlyList<GuildItemViewModel> guilds)
        : base(title)
    {
        _name = character?.Name ?? "";
        _level = character?.Level ?? 1;
        _medals = character?.Medals ?? 0;
        _isMeley = character?.IsMeley ?? false;
        _isGrotte = character?.IsGrotte ?? false;
        _isBalathor = character?.IsBalathor ?? false;
        _isSerpent = character?.IsSerpent ?? false;
        _isDonate = character?.IsDonate ?? false;
        _isBio = character?.IsBio ?? false;
        _bioDone = character?.BioDone ?? false;

        Guilds = new List<GuildItemViewModel>(guilds);
        _guild = character?.Guild ?? Guilds.FirstOrDefault();
    }

    public IReadOnlyList<GuildItemViewModel> Guilds { get; }

    public string Name { get => _name; set => Set(ref _name, value); }
    public int Level { get => _level; set => Set(ref _level, Math.Clamp(value, 1, 120)); }
    public int Medals { get => _medals; set => Set(ref _medals, Math.Max(0, value)); }
    public GuildItemViewModel? Guild { get => _guild; set => Set(ref _guild, value); }

    /// Meley-Char: sein Level wird in der Kachel gruen hinterlegt.
    public bool IsMeley { get => _isMeley; set => Set(ref _isMeley, value); }

    /// Grotte: levelt fuer Meley und andere Laeufe, steht rot in der Kachel.
    public bool IsGrotte { get => _isGrotte; set => Set(ref _isGrotte, value); }

    /// Balathor und Schlangenrun sind eigene Rollen neben Meley und lassen
    /// sich frei damit kombinieren.
    public bool IsBalathor { get => _isBalathor; set => Set(ref _isBalathor, value); }
    public bool IsSerpent { get => _isSerpent; set => Set(ref _isSerpent, value); }

    /// Spenden-Char: die Sammelvergabe kann sich auf diese Rolle beschraenken.
    public bool IsDonate { get => _isDonate; set => Set(ref _isDonate, value); }

    /// Traegt die Orkzahn-Bio dieses Accounts.
    public bool IsBio
    {
        get => _isBio;
        set { if (Set(ref _isBio, value) && !value) BioDone = false; }
    }

    public bool BioDone { get => _bioDone; set => Set(ref _bioDone, value); }

    protected override bool Validate()
    {
        Error = string.IsNullOrWhiteSpace(Name) ? Loc.T("form.needName") : null;
        return !HasError;
    }
}
