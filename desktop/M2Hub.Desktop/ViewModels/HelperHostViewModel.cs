using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Okey;

namespace M2Hub.Desktop.ViewModels;

/// Der Bereich „Helper" haelt mehrere Helfer. Beim Oeffnen zeigt er eine
/// Startseite mit Kacheln; ein Klick oeffnet den jeweiligen Helfer. Weitere
/// Helfer kommen als Kacheln dazu.
public sealed class HelperHostViewModel : ViewModelBase
{
    private object? _current;

    public HelperHostViewModel()
    {
        Okey = new OkeyHelperViewModel();

        OpenCommand = new RelayCommand(p => Open(p as string));
        GoHomeCommand = new RelayCommand(_ => GoHome());

        Tiles.Add(new HelperTile("okey", Loc.T("helper.okey.title"), Loc.T("helper.okey.desc"))
        {
            Icon = OkeyLoot.Icon,
        });
    }

    public OkeyHelperViewModel Okey { get; }

    public ObservableCollection<HelperTile> Tiles { get; } = new();

    public RelayCommand OpenCommand { get; }
    public RelayCommand GoHomeCommand { get; }

    /// Null = Startseite, sonst der offene Helfer.
    public object? Current
    {
        get => _current;
        private set { if (Set(ref _current, value)) Raise(nameof(ShowStart)); }
    }

    public bool ShowStart => _current is null;

    private void Open(string? key)
    {
        Current = key switch
        {
            "okey" => Okey,
            _ => null,
        };
    }

    private void GoHome() => Current = null;

    public void RelabelAfterLanguageChange()
    {
        Okey.RelabelAfterLanguageChange();
        foreach (var t in Tiles)
            if (t.Key == "okey") { t.Title = Loc.T("helper.okey.title"); t.Desc = Loc.T("helper.okey.desc"); }
    }
}

/// Eine Kachel auf der Helper-Startseite.
public sealed class HelperTile(string key, string title, string desc) : ViewModelBase
{
    private string _title = title;
    private string _desc = desc;

    public string Key { get; } = key;
    public string Title { get => _title; set => Set(ref _title, value); }
    public string Desc { get => _desc; set => Set(ref _desc, value); }

    /// Symbol der Kachel (z. B. das Okey-Buch); kann fehlen.
    public Bitmap? Icon { get; init; }

    public bool HasIcon => Icon is not null;
}
