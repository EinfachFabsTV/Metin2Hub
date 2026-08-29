using System.Collections.ObjectModel;
using Avalonia.Media;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

public sealed class CharacterItemViewModel : ViewModelBase
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
    private bool _editingMedals;
    private bool _editingLevel;

    public CharacterItemViewModel(CharacterDto dto, IReadOnlyList<GuildItemViewModel> guilds)
    {
        Id = dto.Id;
        AccountId = dto.AccountId;
        _name = dto.Name;
        _level = dto.Level;
        _medals = dto.Medals;
        _isMeley = dto.IsMeley;
        _isGrotte = dto.IsGrotte;
        _isBalathor = dto.IsBalathor;
        _isSerpent = dto.IsSerpent;
        _isDonate = dto.IsDonate;
        _isBio = dto.IsBio;
        _bioDone = dto.BioDone;
        // Ohne Gilde greift der Platzhalter (Id 0) aus der Auswahlliste.
        _guild = guilds.FirstOrDefault(g => g.Id == (dto.GuildId ?? 0)) ?? guilds.FirstOrDefault(g => g.Id == 0);
    }

    public int Id { get; }
    public int AccountId { get; }

    /// Schnellwahl-Knoepfe dieser Zeile; wird vom AccountsViewModel gefuellt.
    public ObservableCollection<MedalPresetViewModel> Presets { get; } = new();

    public string Name
    {
        get => _name;
        set { if (Set(ref _name, value)) Raise(nameof(Display)); }
    }

    public int Level
    {
        get => _level;
        set => Set(ref _level, Math.Clamp(value, 1, 120));
    }

    public int Medals
    {
        get => _medals;
        set => Set(ref _medals, Math.Max(0, value));
    }

    /// Medaillen werden per Doppelklick in der Zeile bearbeitet - fuer eine
    /// Zahl lohnt keine Maske. Die Zeile zeigt dann ein Eingabefeld statt des
    /// Textes; gespeichert wird beim Verlassen oder mit der Eingabetaste.
    public bool EditingMedals
    {
        get => _editingMedals;
        set { if (Set(ref _editingMedals, value)) Raise(nameof(ShowingMedals)); }
    }

    public bool ShowingMedals => !_editingMedals;

    /// Dasselbe fuer das Level: Doppelklick macht daraus ein Eingabefeld,
    /// Eingabetaste uebernimmt, Escape bricht ab. Das Levelfeld ist beim
    /// Pflegen genauso oft dran wie die Medaillen.
    public bool EditingLevel
    {
        get => _editingLevel;
        set { if (Set(ref _editingLevel, value)) Raise(nameof(ShowingLevel)); }
    }

    public bool ShowingLevel => !_editingLevel;

    public GuildItemViewModel? Guild
    {
        get => _guild;
        set { if (Set(ref _guild, value)) Raise(nameof(GuildName)); }
    }

    public string GuildName => _guild is null || _guild.Id == 0 ? Loc.T("accounts.noGuild") : _guild.Name;

    /* ---------- Rollen ---------- */

    /// Meley-Char. In der Kachel ist sein Level gruen hinterlegt.
    public bool IsMeley
    {
        get => _isMeley;
        set { if (Set(ref _isMeley, value)) Raise(nameof(LevelHighlight)); }
    }

    /// Levelt fuer Meley und andere Laeufe - im Bild rot geschrieben.
    public bool IsGrotte
    {
        get => _isGrotte;
        set { if (Set(ref _isGrotte, value)) Raise(nameof(NameBrush)); }
    }

    /// Balathor-Char. Eigene Rolle neben Meley, frei kombinierbar - ein Char
    /// kann fuer beide Laeufe da sein.
    public bool IsBalathor
    {
        get => _isBalathor;
        set { if (Set(ref _isBalathor, value)) RaiseRunMark(); }
    }

    /// Schlangenrun-Char, ebenso eigenstaendig.
    public bool IsSerpent
    {
        get => _isSerpent;
        set { if (Set(ref _isSerpent, value)) RaiseRunMark(); }
    }

    /// Spenden-Char: auf ihn wird gutgeschrieben, was die Gilde einsammelt.
    /// Eine Rolle wie die anderen - optional und frei kombinierbar.
    public bool IsDonate
    {
        get => _isDonate;
        set { if (Set(ref _isDonate, value)) RaiseRunMark(); }
    }

    /// Kuerzel der Rollen neben dem Namen: „Ba", „Se", „Sp", mehrere mit „·"
    /// verbunden. Meley braucht keins - dort faerbt sich das Level.
    public string RunMark
    {
        get
        {
            var marks = new List<string>(3);
            if (_isBalathor) marks.Add(Loc.T("accounts.role.balathor.short"));
            if (_isSerpent) marks.Add(Loc.T("accounts.role.serpent.short"));
            if (_isDonate) marks.Add(Loc.T("accounts.role.donate.short"));
            return string.Join("·", marks);
        }
    }

    public bool HasRunMark => _isBalathor || _isSerpent || _isDonate;

    private void RaiseRunMark()
    {
        Raise(nameof(RunMark));
        Raise(nameof(HasRunMark));
    }

    /// Traegt die Orkzahn-Bio dieses Accounts.
    public bool IsBio
    {
        get => _isBio;
        set { if (Set(ref _isBio, value)) RaiseBio(); }
    }

    public bool BioDone
    {
        get => _bioDone;
        set { if (Set(ref _bioDone, value)) RaiseBio(); }
    }

    private void RaiseBio()
    {
        Raise(nameof(BioMark));
        Raise(nameof(HasBioMark));
        Raise(nameof(Display));
    }

    /// „*" solange offen, „Bio" sobald erledigt - wie in der Excel-Vorlage.
    public string BioMark => !_isBio ? "" : _bioDone ? "Bio" : "*";
    public bool HasBioMark => _isBio;

    public string Display => BioMark.Length > 0 ? $"{_name} {BioMark}" : _name;

    /// Grotte-Chars stehen rot da, alles andere in der normalen Schrift.
    public IBrush NameBrush => _isGrotte ? Brushes.IndianRed : Brushes.White;

    /// Level gruen hinterlegen, wenn es ein Meley-Char ist.
    public bool LevelHighlight => _isMeley;

    /// Nach einer Schnellwahl kommt der neue Wert direkt aus der Ablage.
    public void SetMedals(int medals)
    {
        _medals = medals;
        Raise(nameof(Medals));
    }

    /// Nach dem Abbrechen einer Eingabe den gespeicherten Stand zurueckholen.
    public void SetLevel(int level)
    {
        _level = level;
        Raise(nameof(Level));
    }

    public void Refresh()
    {
        Raise(nameof(Name));
        Raise(nameof(Display));
        Raise(nameof(Level));
        Raise(nameof(Medals));
        Raise(nameof(GuildName));
        Raise(nameof(NameBrush));
        Raise(nameof(LevelHighlight));
        Raise(nameof(RunMark));
        Raise(nameof(HasRunMark));
        RaiseBio();
    }
}
