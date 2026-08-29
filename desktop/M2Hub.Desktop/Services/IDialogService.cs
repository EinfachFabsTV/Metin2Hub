using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Services;

public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmLabel = "Löschen");

    /// Zeigt die Maske und liefert true, wenn gespeichert wurde.
    Task<bool> EditAccountAsync(AccountEditViewModel model);

    Task<bool> EditCharacterAsync(CharacterEditViewModel model);
}
