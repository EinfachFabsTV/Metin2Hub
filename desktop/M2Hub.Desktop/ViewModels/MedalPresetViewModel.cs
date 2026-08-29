using System.Windows.Input;

namespace M2Hub.Desktop.ViewModels;

/// Ein Schnellwahl-Knopf ("+12") fuer genau einen Charakter.
public sealed class MedalPresetViewModel
{
    public MedalPresetViewModel(string label, int value, CharacterItemViewModel target, Func<CharacterItemViewModel, int, Task> apply)
    {
        Label = label;
        Value = value;
        ApplyCommand = new AsyncRelayCommand(_ => apply(target, value));
    }

    public string Label { get; }
    public int Value { get; }
    public ICommand ApplyCommand { get; }
}

/// Zeile im Schnellwahl-Editor.
public sealed class PresetEditViewModel : ViewModelBase
{
    private string _label;
    private int _value;

    public PresetEditViewModel(string label, int value)
    {
        _label = label;
        _value = value;
    }

    public string Label { get => _label; set => Set(ref _label, value); }
    public int Value { get => _value; set => Set(ref _value, value); }
}
