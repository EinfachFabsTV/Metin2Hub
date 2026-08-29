using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

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
    }

    private void ToggleMaximized() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
}
