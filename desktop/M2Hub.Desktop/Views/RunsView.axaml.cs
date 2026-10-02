using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Views;

public partial class RunsView : UserControl
{
    public RunsView() => AvaloniaXamlLoader.Load(this);

    /// Enter im Truhenfeld traegt den Lauf ein. Sonst muesste man nach jeder
    /// Zahl zur Maus greifen - und ein Abend besteht aus zwanzig Laeufen.
    private void ChestsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not RunsViewModel vm) return;

        vm.AddCommand.Execute(null);
        Commit(e);
    }

    /// Enter im Preisfeld uebernimmt den Preis. Die Bindung greift beim
    /// Verlassen des Feldes - und genau das macht `Commit`.
    private void PriceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        Commit(e);
    }

    /// Nach Enter liegt der Schreibzeiger nicht mehr im Feld.
    ///
    /// Sonst steht es weiter offen: wer danach eine Taste trifft - etwa die 7
    /// fuer den naechsten Lauf - aendert nachtraeglich, was er gerade
    /// bestaetigt hat. Das Verlassen laesst zugleich die Bindung greifen.
    private void Commit(KeyEventArgs e)
    {
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }
}
