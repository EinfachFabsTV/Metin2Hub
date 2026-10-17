using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Platform;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace M2Hub.Desktop.Services;

/// Der Ton am Ende einer Abklingzeit.
///
/// Unter Windows laeuft die Ausgabe ueber **WASAPI** (NAudio). Das alte
/// `PlaySound` aus der winmm folgte nur dem globalen Standardgeraet - bei
/// mehreren Audio-Ausgaben landete der Ton dann auf einem Geraet, das der
/// Nutzer gar nicht hoert, waehrend andere Programme liefen. WASAPI folgt
/// demselben App-Routing wie der Browser. Auf den anderen Plattformen wird
/// weiter das uebliche Abspielprogramm angestossen.
///
/// Die Dateien liegen als `Assets/Sound/timer-<name>.wav` bei (erzeugt von
/// `desktop/tools/timer-sound.py`) und werden beim ersten Mal in den
/// Profilordner gelegt: gelesen wird von der Platte, nicht aus der Exe.
///
/// **Faellt der Ton aus, laeuft die App weiter.** Ein fehlender Lautsprecher
/// ist kein Grund, eine Abklingzeit scheitern zu lassen.
public static class Sound
{
    /// Die drei Toene zur Auswahl. Der Schluessel steht in `TimersData.Sound`
    /// und zugleich im Dateinamen.
    /// Der Standard: tief und rund, faellt auf, ohne zu erschrecken.
    public const string Soft = "weich";

    public const string Chime = "glocke";
    public const string Double = "doppelton";
    public const string Gong = "gong";

    public static readonly string[] All = [Soft, Chime, Double, Gong];

    private static readonly Dictionary<string, string> Paths = new();

    /// Spielt den eingestellten Ton. `name` ist einer aus `All`; was nicht
    /// dabei ist, faellt auf den Standard zurueck - eine umbenannte Datei
    /// soll die Uhr nicht stumm machen.
    ///
    /// Asynchron: der Aufruf kehrt sofort zurueck, damit die Uhr im selben
    /// Takt weiterlaeuft.
    public static void Timer(string? name = null)
    {
        try
        {
            var file = Extract(All.Contains(name) ? name! : Soft);
            if (file is null) return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PlayWasapi(file);
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

    /// Windows: ueber WASAPI (geteilter Modus = folgt dem App-Routing und dem
    /// Standardgeraet des Nutzers). Auf einem Hintergrund-Thread, damit die Uhr
    /// nicht wartet; der Thread spielt die kurze Datei zu Ende und raeumt auf.
    private static void PlayWasapi(string file)
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var reader = new AudioFileReader(file);
                using var output = new WasapiOut(AudioClientShareMode.Shared, 200);
                output.Init(reader);
                output.Play();
                while (output.PlaybackState == PlaybackState.Playing)
                    System.Threading.Thread.Sleep(50);
            }
            catch
            {
                // Faellt die Ausgabe aus, bleibt es still - der Timer zaehlt weiter.
            }
        });
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
