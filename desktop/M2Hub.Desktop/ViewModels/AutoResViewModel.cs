using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Auto-Res: Aufloesungs-Presets fuer die metin2.cfg. Ein Preset haelt eine
/// vollstaendige Config (mit seiner Aufloesung); „Setzen" ersetzt damit die
/// Ziel-cfg. Geaendert wird nur WIDTH/HEIGHT; den Client startet man selbst.
public sealed class AutoResViewModel : ViewModelBase
{
    private readonly LocalStore _store;

    public AutoResViewModel(LocalStore store)
    {
        _store = store;
        _configPath = store.Settings.AutoRes.ConfigPath;
        foreach (var p in store.Settings.AutoRes.Presets)
            Presets.Add(new ResPresetVM(this, p));

        AddCommand = new RelayCommand(_ => AddPreset());
        RefreshCurrent();
    }

    /* ---------- Ziel-Config ---------- */

    private string _configPath;
    public string ConfigPath
    {
        get => _configPath;
        set
        {
            if (!Set(ref _configPath, value)) return;
            _store.Settings.AutoRes.ConfigPath = value ?? "";
            _store.SaveSettings();
            RefreshCurrent();
            Raise(nameof(HasConfig));
        }
    }

    public bool HasConfig => !string.IsNullOrWhiteSpace(_configPath) && File.Exists(_configPath);

    private string _currentResText = "";
    public string CurrentResText { get => _currentResText; private set => Set(ref _currentResText, value); }

    private void RefreshCurrent()
    {
        var r = AutoResConfig.Read(_configPath);
        CurrentResText = r is { } v ? $"{v.Width} × {v.Height}" : Loc.T("autores.noRes");
        Raise(nameof(HasConfig));
    }

    /* ---------- Presets ---------- */

    public ObservableCollection<ResPresetVM> Presets { get; } = new();

    private string _newName = "";
    private int _newWidth = 800;
    private int _newHeight = 600;

    public string NewName { get => _newName; set => Set(ref _newName, value); }
    public int NewWidth { get => _newWidth; set => Set(ref _newWidth, value); }
    public int NewHeight { get => _newHeight; set => Set(ref _newHeight, value); }

    public RelayCommand AddCommand { get; }

    private void AddPreset()
    {
        if (!HasConfig) { Status = Loc.T("autores.needConfig"); return; }

        var name = string.IsNullOrWhiteSpace(_newName) ? $"{_newWidth}x{_newHeight}" : _newName.Trim();
        var file = Slug(name) + "-" + Guid.NewGuid().ToString("N")[..6] + ".cfg";

        if (!AutoResConfig.SaveSnapshot(_configPath, _newWidth, _newHeight, file))
        {
            Status = Loc.T("autores.needConfig");
            return;
        }

        var data = new ResPreset { Name = name, Width = _newWidth, Height = _newHeight, File = file };
        _store.Settings.AutoRes.Presets.Add(data);
        _store.SaveSettings();
        Presets.Add(new ResPresetVM(this, data));

        NewName = "";
        Status = Loc.T("autores.added", name);
    }

    public void Apply(ResPresetVM preset)
    {
        if (AutoResConfig.Apply(preset.File, _configPath))
        {
            RefreshCurrent();
            Status = Loc.T("autores.applied", preset.Name, preset.Width, preset.Height);
        }
        else
        {
            Status = Loc.T("autores.applyFail");
        }
    }

    public void DeletePreset(ResPresetVM preset)
    {
        AutoResConfig.Delete(preset.File);
        _store.Settings.AutoRes.Presets.RemoveAll(p => p.File == preset.File);
        _store.SaveSettings();
        Presets.Remove(preset);
    }

    private string _status = "";
    public string Status { get => _status; private set { if (Set(ref _status, value)) Raise(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrEmpty(_status);

    public void RelabelAfterLanguageChange()
    {
        RefreshCurrent();
    }

    private static string Slug(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        var slug = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "preset" : slug;
    }
}

/// Ein Aufloesungs-Preset in der Oberflaeche.
public sealed class ResPresetVM
{
    public ResPresetVM(AutoResViewModel host, ResPreset data)
    {
        Name = data.Name;
        Width = data.Width;
        Height = data.Height;
        File = data.File;
        PlayCommand = new RelayCommand(_ => host.Apply(this));
        DeleteCommand = new RelayCommand(_ => host.DeletePreset(this));
    }

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public string File { get; }
    public string ResText => $"{Width} × {Height}";

    public RelayCommand PlayCommand { get; }
    public RelayCommand DeleteCommand { get; }
}
