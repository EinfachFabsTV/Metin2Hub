using System.Windows.Input;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Hinweis auf eine neue Version samt Installation.
///
/// Die App laedt die neue Programmdatei neben die laufende, tauscht beide und
/// startet neu. Klappt das nicht - etwa weil die Exe in einem geschuetzten
/// Ordner liegt - bleibt der Weg ueber die Release-Seite im Browser.
public sealed class UpdateDialogViewModel : DialogViewModelBase
{
    private readonly UpdateService _updates;
    private readonly Action _restart;

    private bool _busy;
    private double _progress;
    private string _status = "";

    public UpdateDialogViewModel(UpdateService updates, UpdateService.UpdateInfo info, Action restart)
        : base(Loc.T("update.title", info.Version))
    {
        _updates = updates;
        _restart = restart;
        Info = info;

        CanInstall = info.CanInstall && UpdateService.CanReplaceInPlace();
        Notes = Shorten(info.Notes);

        InstallCommand = new AsyncRelayCommand(_ => InstallAsync());
        OpenPageCommand = new RelayCommand(_ =>
        {
            Platform.OpenUrl(info.Url);
            Close(true);
        });
        LaterCommand = CancelCommand;
    }

    public UpdateService.UpdateInfo Info { get; }

    public string Current => Loc.T("update.current", UpdateService.CurrentVersion);
    public string Notes { get; }
    public bool HasNotes => Notes.Length > 0;

    /// Nur wenn die Exe an ihrem Platz ersetzt werden darf.
    public bool CanInstall { get; }
    public bool CannotInstall => !CanInstall;

    public string SizeHint => Info.SizeText.Length > 0
        ? Loc.T("update.size", Info.SizeText)
        : "";

    public bool Busy
    {
        get => _busy;
        private set { if (Set(ref _busy, value)) Raise(nameof(NotBusy)); }
    }

    public bool NotBusy => !_busy;

    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    public string Status
    {
        get => _status;
        private set { if (Set(ref _status, value)) Raise(nameof(HasStatus)); }
    }

    public bool HasStatus => _status.Length > 0;

    public ICommand InstallCommand { get; }
    public ICommand OpenPageCommand { get; }
    public ICommand LaterCommand { get; }

    private async Task InstallAsync()
    {
        Busy = true;
        Status = Loc.T("update.downloading");
        Progress = 0;
        Error = null;

        var staged = await _updates.DownloadAsync(Info, new Progress<double>(p => Progress = p));

        if (staged is null)
        {
            Busy = false;
            Status = "";
            Error = Loc.T("update.failedDownload");
            return;
        }

        Status = Loc.T("update.replacing");

        if (!UpdateService.ApplyAndRestart(staged))
        {
            Busy = false;
            Status = "";
            Error = Loc.T("update.failedReplace");
            return;
        }

        // Der Nachfolger laeuft bereits - dieses Programm macht Platz.
        _restart();
    }

    private static string Shorten(string notes) =>
        notes.Length <= 300 ? notes : notes[..300].TrimEnd() + " …";
}
