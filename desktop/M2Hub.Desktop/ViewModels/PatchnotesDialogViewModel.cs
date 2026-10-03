using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// „Was ist neu" nach einem Update - einmal je Version.
///
/// Kein Hinweis auf etwas, das man erst holen muss: die Fassung laeuft bereits.
/// Deshalb nur lesen und wegklicken.
public sealed class PatchnotesDialogViewModel : DialogViewModelBase
{
    public PatchnotesDialogViewModel(string version, string notes)
        : base(Loc.T("patchnotes.title"))
    {
        Version = Loc.T("patchnotes.version", version);
        Notes = notes;
    }

    /// „Patch 1.50.1" - unter der Ueberschrift, vor dem Inhalt.
    public string Version { get; }

    public string Notes { get; }
}
