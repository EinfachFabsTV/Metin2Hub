using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Views;

/// Die Abklingzeit-Knoepfe als eigenes Fenster ueber dem Spiel.
///
/// **Immer oben, fuer jedes Programm.** Windows kennt kein „nur ueber Metin2";
/// wer das Fenster waehrend des Streams nicht sehen will, blendet es mit dem
/// Kreuz oder ueber die Seitenleiste aus.
///
/// Keine Leiste vom Betriebssystem (`SystemDecorations=None`): sie waere im
/// Spiel nur im Weg. **Gezogen wird an der ganzen Flaeche** - im Spiel sucht
/// man keinen Griff -, und wo das Fenster stand, merkt sich `TimersData`.
public partial class TimerOverlayWindow : Window
{
    private readonly LocalStore? _store;

    /// Avalonia braucht einen parameterlosen Konstruktor fuer den Designer.
    public TimerOverlayWindow() : this(null) { }

    public TimerOverlayWindow(LocalStore? store)
    {
        _store = store;
        AvaloniaXamlLoader.Load(this);

        // Gezogen wird an der ganzen Flaeche, nicht nur an der Leiste: im
        // Spiel sucht man keinen Griff. Die Knoepfe nehmen den Klick vorher
        // selbst, hier kommt nur an, was daneben liegt.
        if (this.FindControl<Border>("Frame") is { } frame)
            frame.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
            };

        if (this.FindControl<Button>("HideButton") is { } hide)
            hide.Click += (_, _) => HideAndRemember();

        if (_store is { } s)
        {
            Position = new PixelPoint((int)s.Settings.Timers.X, (int)s.Settings.Timers.Y);

            // Die Stelle wird beim Ziehen nur gemerkt und erst beim
            // Ausblenden oder Schliessen geschrieben - ein Zug quer ueber den
            // Bildschirm schriebe sonst hundert Dateien.
            PositionChanged += (_, _) =>
            {
                s.Settings.Timers.X = Position.X;
                s.Settings.Timers.Y = Position.Y;
            };

            Closing += (_, _) => Remember();
        }
    }

    /// Linksklick startet (das macht der Knopf selbst), **Rechtsklick haelt
    /// an**: ein abgebrochener Lauf soll keine Uhr hinterlassen, die
    /// weiterzaehlt.
    private void TilePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        if (sender is not Button { DataContext: RunTimerViewModel timer }) return;
        if (DataContext is not RunTimersViewModel vm) return;

        vm.StopCommand.Execute(timer);
        e.Handled = true;
    }

    private void HideAndRemember()
    {
        if (_store is { } s) s.Settings.Timers.Enabled = false;

        Remember();
        Hide();
    }

    private void Remember() => _store?.SaveSettings();
}
