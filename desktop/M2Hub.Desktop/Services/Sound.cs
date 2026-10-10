using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Platform;

namespace M2Hub.Desktop.Services;

/// Der Ton am Ende einer Abklingzeit.
///
/// Avalonia bringt keine Tonausgabe mit, und ein Audio-Paket waere fuer zwei
/// Glockentoene zu viel. Unter Windows uebernimmt `PlaySound` aus der winmm,
/// die zu Windows selbst gehoert; auf den anderen Plattformen wird das
/// uebliche Abspielprogramm angestossen.
///
/// Die Dateien liegen als `Assets/Sound/timer-<name>.wav` bei (erzeugt von
/// `desktop/tools/timer-sound.py`) und werden beim ersten Mal in den
/// Profilordner gelegt: `PlaySound` liest von der Platte, nicht aus der Exe.
///
/// **Faellt der Ton aus, laeuft die App weiter.** Ein fehlender Lautsprecher
/// ist kein Grund, eine Abklingzeit scheitern zu lassen.
public static class Sound
{
    /// Die drei Toene zur Auswahl. Der Schluessel steht in `TimersData.Sound`
    /// und zugleich im Dateinamen.
    public const string Chime = "glocke";
    public const string Double = "doppelton";
    public const string Gong = "gong";

    public static readonly string[] All = [Chime, Double, Gong];

    private static readonly Dictionary<string, string> Paths = new();

    /// Asynchron - der Aufruf kehrt sofort zurueck, damit die Uhr im selben
    /// Takt weiterlaeuft.
    ///
    /// **Ohne SND_NOSTOP**: das Flag liess `PlaySound` stillschweigend nichts
    /// tun, sobald irgendein anderer Ton lief - deshalb blieb der Ton in
    /// 1.56.0 oft ganz aus.
    private const uint Async = 0x0001;
    private const uint FileName = 0x00020000;

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);

    /// Spielt den eingestellten Ton. `name` ist einer aus `All`; was nicht
    /// dabei ist, faellt auf die Glocke zurueck - eine umbenannte Datei soll
    /// die Uhr nicht stumm machen.
    public static void Timer(string? name = null)
    {
        try
        {
            var file = Extract(All.Contains(name) ? name! : Chime);
            if (file is null) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PlaySound(file, IntPtr.Zero, Async | FileName);
                return;
            }

            var player = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "afplay" : "paplay";

            Process.Start(new ProcessStartInfo(player, '"' + file + '"')
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch
        {
            // Kein Lautsprecher, kein Abspielprogramm, gesperrte Datei - der
            // Timer ist wichtiger als sein Ton.
        }
    }

    /// Legt die Datei einmal im Profil ab und merkt sich den Weg dorthin.
    private static string? Extract(string name)
    {
        if (Paths.TryGetValue(name, out var known) && File.Exists(known)) return known;

        try
        {
            var dir = Path.Combine(LocalStore.Directory, "sound");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, "timer-" + name + ".wav");
            if (!File.Exists(file))
            {
                using var stream = AssetLoader.Open(
                    new Uri($"avares://M2Hub/Assets/Sound/timer-{name}.wav"));
                using var target = File.Create(file);
                stream.CopyTo(target);
            }

            return Paths[name] = file;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
