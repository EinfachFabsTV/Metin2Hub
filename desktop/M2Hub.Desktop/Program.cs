using Avalonia;

namespace M2Hub.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Avalonia zeichnet unter Windows sonst ueber die
            // WinUI-Komposition. Das Fenster hat dann keine
            // Weiterleitungsflaeche (WS_EX_NOREDIRECTIONBITMAP), und
            // Fensteraufnahmen greifen ins Leere: OBS bietet das Fenster mit
            // der aelteren Methode gar nicht erst an, mit der neueren bleibt
            // es schwarz. Mit der Weiterleitungsflaeche ist es ein
            // gewoehnliches Fenster und laesst sich aufnehmen.
            //
            // Gekostet hat das nichts: durchscheinende Fenster nutzt die App
            // nicht, das Thema ist ohnehin deckend (TransparencyLevelHint
            // steht auf None).
            .With(new Win32PlatformOptions
            {
                CompositionMode = [Win32CompositionMode.RedirectionSurface],
            })
            .WithInterFont()
            .LogToTrace();
}
