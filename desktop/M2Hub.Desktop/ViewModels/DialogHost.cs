using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Haelt die gerade offene Maske. Die Ansicht legt sie ueber den Inhalt,
/// statt ein zweites Fenster zu oeffnen.
public sealed class DialogHost : ViewModelBase, IDialogService
{
    private DialogViewModelBase? _current;

    public DialogViewModelBase? Current
    {
        get => _current;
        private set { if (Set(ref _current, value)) Raise(nameof(HasDialog)); }
    }

    public bool HasDialog => _current is not null;

    /// Zeigt die Maske und wartet, bis sie geschlossen wird.
    public async Task<bool> ShowAsync(DialogViewModelBase dialog)
    {
        // Eine bereits offene Maske wird abgebrochen - zwei uebereinander
        // waeren nicht bedienbar.
        _current?.Close(false);

        Current = dialog;
        try
        {
            return await dialog.Completion;
        }
        finally
        {
            if (ReferenceEquals(Current, dialog)) Current = null;
        }
    }

    public Task<bool> ConfirmAsync(string title, string message, string? confirmLabel = null) =>
        ShowAsync(new ConfirmDialogViewModel(title, message, confirmLabel ?? Loc.T("common.delete")));

    public Task<bool> EditAccountAsync(AccountEditViewModel model) => ShowAsync(model);

    public Task<bool> EditCharacterAsync(CharacterEditViewModel model) => ShowAsync(model);
}
