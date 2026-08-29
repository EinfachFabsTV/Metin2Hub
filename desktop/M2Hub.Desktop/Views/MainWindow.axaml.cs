using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        // Die Fensterleiste des Betriebssystems ist abgeschaltet
        // (ExtendClientAreaToDecorationsHint), deshalb bedient die Kopfzeile
        // Minimieren, Maximieren, Schliessen und das Verschieben selbst.
        this.FindControl<Button>("MinimizeButton")!.Click += (_, _) =>
            WindowState = WindowState.Minimized;

        this.FindControl<Button>("MaximizeButton")!.Click += (_, _) => ToggleMaximized();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Close();

        if (this.FindControl<Border>("TitleBar") is { } titleBar)
        {
            titleBar.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

                // Doppelklick auf die Kopfzeile schaltet wie gewohnt um
                if (e.ClickCount == 2)
                {
                    ToggleMaximized();
                    return;
                }

                BeginMoveDrag(e);
            };
        }

        // Tastatur vor allen Steuerelementen: eine offene Maske soll mit
        // Escape schliessen und mit der Eingabetaste bestaetigen, gleich
        // worauf die Eingabe gerade liegt. Deshalb im Tunnel, nicht im
        // Bubble - ein TextBox verschluckt die Tasten sonst.
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Offene Maske: Escape bricht ab, Enter bestaetigt.
        if (vm.Dialogs.Current is { } dialog)
        {
            if (e.Key == Key.Escape)
            {
                dialog.CancelCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && !InMultilineText())
            {
                dialog.ConfirmCommand.Execute(null);
                e.Handled = true;
            }
            return;
        }

        // Ohne Maske raeumt Escape die Ansicht auf: das eingeklappte
        // Verwaltungsfeld zu, sonst die Suche leer.
        if (e.Key == Key.Escape && vm.CurrentPage is AccountsViewModel accounts)
        {
            if (accounts.Cancel()) e.Handled = true;
            return;
        }

        // Reiter: mit den Pfeiltasten zum Nachbarn, solange die Eingabe auf
        // der Leiste selbst liegt. Sonst gehoeren die Pfeile dem Feld, in dem
        // gerade geschrieben oder ausgewaehlt wird.
        if (e.Key is Key.Left or Key.Right && OnNavBar())
        {
            vm.ShowNeighbour(e.Key == Key.Left ? -1 : +1);
            e.Handled = true;
        }
    }

    /// Ein mehrzeiliges Feld (die Notiz) braucht die Eingabetaste selbst.
    private bool InMultilineText() =>
        FocusManager?.GetFocusedElement() is TextBox { AcceptsReturn: true };

    private bool OnNavBar()
    {
        if (FocusManager?.GetFocusedElement() is not Visual focused) return false;
        var bar = this.FindControl<StackPanel>("NavBar");
        for (var v = focused; v is not null; v = v.GetVisualParent())
            if (ReferenceEquals(v, bar)) return true;
        return false;
    }

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
}
