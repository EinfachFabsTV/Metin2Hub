using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Avalonia.Media;
using M2Hub.Desktop.Models;

namespace M2Hub.Desktop.ViewModels;

/// Eine Account-Kachel: Kopfzeile, Charaktere, Bio-Stand und die Kurzkennung.
public sealed partial class AccountItemViewModel : ViewModelBase
{
    private string _name;
    private string _note;
    private int _dragonCoins;
    private LanguageItemViewModel? _language;
    private ServerOption? _server;

    public AccountItemViewModel(
        AccountDto dto,
        IReadOnlyList<GuildItemViewModel> guilds,
        IReadOnlyList<LanguageItemViewModel> languages)
    {
        Id = dto.Id;
        _name = dto.Name;
        _note = dto.Note;
        _dragonCoins = dto.DragonCoins;
        _language = languages.FirstOrDefault(l => l.Id == (dto.LanguageId ?? 0));
        _server = dto.ServerKey.Length == 0
            ? null
            : new ServerOption(dto.ServerKey,
                dto.ServerLabel.Length > 0 ? dto.ServerLabel : dto.ServerKey);

        foreach (var c in dto.Characters.OrderBy(c => c.Sort).ThenBy(c => c.Id))
            Characters.Add(new CharacterItemViewModel(c, guilds));

        Recount();
    }

    public int Id { get; }

    public string Name
    {
        get => _name;
        set { if (Set(ref _name, value)) Raise(nameof(ShortLabel)); }
    }

    public string Note
    {
        get => _note;
        set { if (Set(ref _note, value)) Raise(nameof(HasNote)); }
    }

    public bool HasNote => !string.IsNullOrWhiteSpace(_note);

    public int DragonCoins
    {
        get => _dragonCoins;
        set { if (Set(ref _dragonCoins, Math.Max(0, value))) { Raise(nameof(CoinLabel)); Raise(nameof(HasCoins)); } }
    }

    /// Nur die Zahl - das Muenzsymbol steht daneben.
    public string CoinLabel => _dragonCoins.ToString();
    public bool HasCoins => _dragonCoins > 0;

    public LanguageItemViewModel? Language
    {
        get => _language;
        set { if (Set(ref _language, value)) { Raise(nameof(NameBrush)); Raise(nameof(LanguageName)); } }
    }

    /// Server, auf dem der Account spielt - null heisst: keiner zugeordnet.
    public ServerOption? Server
    {
        get => _server;
        set { if (Set(ref _server, value)) { Raise(nameof(ServerLabel)); Raise(nameof(HasServer)); } }
    }

    public string ServerKey => _server?.Key ?? "";
    public string ServerLabel => _server?.Label ?? "";
    public bool HasServer => _server is not null && _server.IsRealServer;

    public string LanguageName => _language?.Name ?? "";
    public bool HasLanguage => _language is not null && _language.Id != 0;

    /// Der Accountname traegt die Farbe seiner Client-Sprache.
    public IBrush NameBrush => _language is null || _language.Id == 0 ? Brushes.White : _language.Brush;

    public ObservableCollection<CharacterItemViewModel> Characters { get; } = new();

    [GeneratedRegex(@"(\d{4,})$")]
    private static partial Regex TrailingDigits();

    /// Grosse Kennung unten in der Kachel. Endet der Accountname auf Ziffern,
    /// genuegen die letzten vier - sonst sagt nur der ganze Name etwas.
    public string ShortLabel
    {
        get
        {
            var m = TrailingDigits().Match(_name ?? "");
            if (!m.Success) return _name ?? "";
            var digits = m.Groups[1].Value;
            return digits[^4..];
        }
    }

    /* ---------- Abgeleitetes ---------- */

    private int _characterCount;
    private int _medals;

    public int CharacterCount { get => _characterCount; private set => Set(ref _characterCount, value); }
    public int Medals { get => _medals; private set => Set(ref _medals, value); }

    /// Die Orkzahn-Bio gilt je Account: erledigt, sobald ein Bio-Char sie
    /// abgeschlossen hat.
    public bool BioDone => Characters.Any(c => c.IsBio && c.BioDone);
    public bool BioPending => Characters.Any(c => c.IsBio) && !BioDone;
    public string BioLabel => BioDone ? "Bio fertig" : "Bio nicht fertig";

    public string Summary => $"{CharacterCount} Chars · {Medals} Medaillen";

    public void Recount()
    {
        CharacterCount = Characters.Count;
        Medals = Characters.Sum(c => c.Medals);
        Raise(nameof(Summary));
        Raise(nameof(BioDone));
        Raise(nameof(BioPending));
        Raise(nameof(BioLabel));
    }
}
