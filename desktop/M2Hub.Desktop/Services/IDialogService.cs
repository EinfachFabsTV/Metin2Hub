using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string? confirmLabel = null);

    /// Zeigt eine beliebige Maske - etwa die Hilfe, die nur gelesen wird.
    Task<bool> ShowAsync(DialogViewModelBase dialog);

    /// Zeigt die Maske und liefert true, wenn gespeichert wurde.
    Task<bool> EditAccountAsync(AccountEditViewModel model);

    Task<bool> EditCharacterAsync(CharacterEditViewModel model);
}
