using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Views;

public partial class AccountsView : UserControl
{
    public AccountsView() => AvaloniaXamlLoader.Load(this);

    /// Doppelklick auf die Medaillen macht aus der Zahl ein Eingabefeld.
    /// Fuer eine einzelne Zahl lohnt keine Maske.
    private void MedalsDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is CharacterItemViewModel character)
            character.EditingMedals = true;
    }

    /// Dasselbe fuer das Level - beim Pflegen ist es genauso oft dran.
    private void LevelDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is CharacterItemViewModel character)
            character.EditingLevel = true;
    }

    /// Verlassen des Feldes uebernimmt den Wert.
    private void MedalsCommitted(object? sender, RoutedEventArgs e) => Run(sender, vm => vm.SetMedalsCommand);
    private void LevelCommitted(object? sender, RoutedEventArgs e) => Run(sender, vm => vm.SetLevelCommand);

    /// Eingabetaste uebernimmt, Escape verwirft und holt den alten Wert zurueck.
    private void MedalsKeyDown(object? sender, KeyEventArgs e) =>
        EditKeyDown(sender, e, vm => vm.SetMedalsCommand, vm => vm.CancelMedalsCommand);

    private void LevelKeyDown(object? sender, KeyEventArgs e) =>
        EditKeyDown(sender, e, vm => vm.SetLevelCommand, vm => vm.CancelLevelCommand);

    private void EditKeyDown(
        object? sender,
        KeyEventArgs e,
        Func<AccountsViewModel, RelayCommand> commit,
        Func<AccountsViewModel, RelayCommand> cancel)
    {
        if (e.Key == Key.Enter) { Run(sender, commit); e.Handled = true; }
        else if (e.Key == Key.Escape) { Run(sender, cancel); e.Handled = true; }
    }

    private void Run(object? sender, Func<AccountsViewModel, RelayCommand> pick)
    {
        if ((sender as Control)?.DataContext is not CharacterItemViewModel character) return;
        if (DataContext is AccountsViewModel vm) pick(vm).Execute(character);
    }

    /* ---------- Kacheln ziehen ---------- */

    /// Kennung des gezogenen Accounts. Ein eigenes Format, damit nichts anderes
    /// (Text aus einem Feld, eine Datei) versehentlich als Kachel gilt.
    private const string TileFormat = "m2hub/account";

    private AccountItemViewModel? _dragged;
    private Point _pressed;

    private void TilePressed(object? sender, PointerPressedEventArgs e)
    {
        // Nur die linke Taste, und nur wenn die eigene Reihenfolge gilt -
        // sonst waere das Ergebnis beim naechsten Sortieren wieder weg.
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (DataContext is not AccountsViewModel { CanReorder: true }) return;

        // In einem Eingabefeld zieht man Text, nicht die Kachel.
        if (InsideInput(e.Source as Visual)) return;

        _dragged = (sender as Control)?.DataContext as AccountItemViewModel;
        _pressed = e.GetPosition(this);
    }

    private async void TileMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged is null || sender is not Control tile) return;

        // Erst ab ein paar Pixeln ziehen - sonst loest jeder Klick auf einen
        // Knopf in der Kachel einen Ziehvorgang aus.
        var moved = e.GetPosition(this) - _pressed;
        if (Math.Abs(moved.X) < 6 && Math.Abs(moved.Y) < 6) return;

        var data = new DataObject();
        data.Set(TileFormat, _dragged);
        _dragged = null;

        // Die gezogene Kachel blasser zeichnen, damit sichtbar ist, was haengt.
        tile.Opacity = 0.5;
        try
        {
            await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        }
        finally
        {
            tile.Opacity = 1;
        }
    }

    private void TileReleased(object? sender, PointerReleasedEventArgs e) => _dragged = null;

    private static bool InsideInput(Visual? source)
    {
        for (var v = source; v is not null; v = v.GetVisualParent())
            if (v is TextBox or NumericUpDown) return true;
        return false;
    }

    private void TileDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(TileFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void TileDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not AccountsViewModel vm) return;
        if (e.Data.Get(TileFormat) is not AccountItemViewModel source) return;
        if ((sender as Control)?.DataContext is not AccountItemViewModel target) return;

        vm.MoveBefore(source, target);
        e.Handled = true;
    }
}
