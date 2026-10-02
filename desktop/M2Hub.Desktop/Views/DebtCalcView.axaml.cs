using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace M2Hub.Desktop.Views;

public partial class DebtCalcView : UserControl
{
    public DebtCalcView() => AvaloniaXamlLoader.Load(this);

    /// Enter uebernimmt die Zahl und gibt das Feld frei.
    ///
    /// Die Bindungen greifen beim Verlassen des Feldes; der Schreibzeiger
    /// wandert also hinaus, und die Eingabe steht. Bliebe er stehen, wuerde
    /// die naechste Taste nachtraeglich aendern, was man gerade bestaetigt
    /// hat.
    private void CommitKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        TopLevel.GetTopLevel(this)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }
}
