using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Views;

public partial class GoalsView : UserControl
{
    public GoalsView() => AvaloniaXamlLoader.Load(this);

    /// Enter traegt den Posten ein - wie im Run Tracker, und danach liegt der
    /// Schreibzeiger nicht mehr im Feld.
    private void AddKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not GoalsViewModel vm) return;

        vm.AddBookingCommand.Execute(null);
        Commit(e);
    }

    /// Enter haengt die Truhenzeile an - der Posten selbst wird erst mit
    /// „Eintragen" gebucht, sonst waere die erste Zeile zugleich der Abschluss.
    private void AddChestKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not GoalsViewModel vm) return;

        vm.AddChestCommand.Execute(null);
        Commit(e);
    }

    /// Enter legt das Ziel an.
    private void AddGoalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not GoalsViewModel vm) return;

        vm.AddGoalCommand.Execute(null);
        Commit(e);
    }

    private void Commit(KeyEventArgs e)
    {
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }
}
