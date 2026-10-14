using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

namespace M2Hub.Desktop.Views;

public partial class PetCalcView : UserControl
{
    public PetCalcView() => AvaloniaXamlLoader.Load(this);

    /// Enter übernimmt die Zahl und gibt das Feld frei (die Bindung greift auf
    /// LostFocus). Wie im Run Tracker und Schulden-Rechner.
    private void CommitKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }

    /// Klick irgendwo außerhalb eines Textfeldes bestätigt die Eingabe ebenso.
    private void RootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual v && v.FindAncestorOfType<TextBox>(includeSelf: true) is not null) return;
        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
    }
}
