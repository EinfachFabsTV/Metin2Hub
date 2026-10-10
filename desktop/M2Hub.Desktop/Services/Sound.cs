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
/// Die Datei liegt als `Assets/Sound/timer.wav` bei (erzeugt von
/// `desktop/tools/timer-sound.py`) und wird beim ersten Mal in den
/// Profilordner gelegt: `PlaySound` liest von der Platte, nicht aus der Exe.
///
/// **Faellt der Ton aus, laeuft die App weiter.** Ein fehlender Lautsprecher
/// ist kein Grund, eine Abklingzeit scheitern zu lassen.
public static class Sound
{
    private const string Asset = "avares://M2Hub/Assets/Sound/timer.wav";

    private static string? _path;

    /// Asynchron, aus dem Speicher, ohne Schleife - der Aufruf kehrt sofort
    /// zurueck, damit die Uhr im selben Takt weiterlaeuft.
    private const uint Async = 0x0001;
    private const uint FileName = 0x00020000;
    private const uint NoStop = 0x0010;

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);

    public static void Timer()
    {
        try
        {
            var file = Extract();
            if (file is null) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PlaySound(file, IntPtr.Zero, Async | FileName | NoStop);
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
    private static string? Extract()
    {
        if (_path is { } known && File.Exists(known)) return known;

        try
        {
            var dir = Path.Combine(LocalStore.Directory, "sound");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, "timer.wav");
            if (!File.Exists(file))
            {
                using var stream = AssetLoader.Open(new Uri(Asset));
                using var target = File.Create(file);
                stream.CopyTo(target);
            }

            return _path = file;
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }
}
