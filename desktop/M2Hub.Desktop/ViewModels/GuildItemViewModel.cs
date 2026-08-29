using M2Hub.Desktop.Models;

namespace M2Hub.Desktop.ViewModels;

public sealed class GuildItemViewModel : ViewModelBase
{
    private string _name;
    private int _level;

    public GuildItemViewModel(GuildDto dto)
    {
        Id = dto.Id;
        _name = dto.Name;
        _level = dto.Level;
    }

    public int Id { get; }

    public string Name { get => _name; set { if (Set(ref _name, value)) Raise(nameof(Display)); } }
    public int Level { get => _level; set { if (Set(ref _level, value)) Raise(nameof(Display)); } }

    public string Display => $"{Name} (Lv {Level})";

    private int _medals;
    /// Summe der Medaillen aller zugeordneten Charaktere.
    public int Medals { get => _medals; set => Set(ref _medals, value); }
}
