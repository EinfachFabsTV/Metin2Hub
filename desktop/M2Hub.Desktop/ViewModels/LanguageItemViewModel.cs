using Avalonia.Media;
using M2Hub.Desktop.Models;

namespace M2Hub.Desktop.ViewModels;

/// Eine Client-Sprache mit ihrer Farbe.
public sealed class LanguageItemViewModel : ViewModelBase
{
    private string _name;
    private string _color;

    public LanguageItemViewModel(LanguageDto dto)
    {
        Id = dto.Id;
        _name = dto.Name;
        _color = dto.Color;
    }

    public int Id { get; }

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string Color
    {
        get => _color;
        set { if (Set(ref _color, value)) Raise(nameof(Brush)); }
    }

    /// Unbekannte oder leere Farbwerte duerfen die Ansicht nicht sprengen.
    public IBrush Brush =>
        Avalonia.Media.Color.TryParse(_color, out var c)
            ? new SolidColorBrush(c)
            : Brushes.Gainsboro;

    public bool IsPlaceholder => Id == 0;
}
