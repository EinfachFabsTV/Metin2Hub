using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace M2Hub.Desktop.Views;

public partial class RunsView : UserControl
{
    public RunsView() => AvaloniaXamlLoader.Load(this);

    /// Die Monatsauswahl oeffnet ihre Liste sonst erst beim Tippen: wer sie
    /// anklickt, sieht nicht, welche Monate es ueberhaupt gibt. Beim
    /// Hineinklicken wird deshalb der Text geleert - ein leeres Suchwort
    /// passt auf alles - und die Liste aufgeklappt.
    private void MonthPickOpen(object? sender, GotFocusEventArgs e)
    {
        if (sender is not AutoCompleteBox box) return;

        box.Text = string.Empty;
        box.IsDropDownOpen = true;
    }

    /// Beim Verlassen steht wieder der gewaehlte Monat im Feld - sonst bliebe
    /// es leer, obwohl die Statistik einen Monat zeigt.
    private void MonthPickClose(object? sender, RoutedEventArgs e)
    {
        if (sender is not AutoCompleteBox box) return;

        box.Text = box.SelectedItem?.ToString() ?? string.Empty;
    }
}
