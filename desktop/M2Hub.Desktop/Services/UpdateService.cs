using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace M2Hub.Desktop.Services;

/// Sieht bei GitHub nach, ob es eine neuere Version gibt.
///
/// Gefragt wird das **oeffentliche** Release-Repo, nicht das private Hauptrepo:
/// die Anfrage laeuft ohne Anmeldung, und ein eingebauter Zugangsschluessel in
/// einer weitergegebenen Exe waere ein Schluessel fuer jeden, der die Datei
/// hat. Verglichen wird die Versionsnummer des neuesten Releases mit der
/// eingebauten.
/// Heruntergeladen wird nichts von selbst - die App oeffnet auf Wunsch die
/// Release-Seite im Browser. Ein stiller Selbst-Austausch der laufenden Exe
/// waere hier das falsche Mittel: er braucht Schreibrechte im Programmordner
/// und laesst sich nicht zurueckdrehen.
public sealed class UpdateService
{
    private const string ReleaseApi =
        "https://api.github.com/repos/EinfachFabsTV/Metin2Hub/releases/latest";

    public const string ReleasePage =
        "https://github.com/EinfachFabsTV/Metin2Hub/releases/latest";

    private readonly HttpClient _http;

    public UpdateService()
    {
        _http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        // GitHub verlangt einen User-Agent, sonst kommt 403 zurueck.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"M2Hub-Desktop/{CurrentVersion}");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    /// Version dieser Exe, wie sie im Projekt gesetzt ist.
    ///
    /// Gelesen wird die InformationalVersion - sie traegt <Version> aus der
    /// csproj unveraendert. GetName().Version stammt dagegen aus
    /// AssemblyVersion, die auch abweichen kann; genau daran hing die App
    /// frueher fest und hielt sich dauerhaft fuer veraltet.
    public static string CurrentVersion { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informational is not null)
        {
            // Beim Bauen aus einem Git-Stand haengt ein "+<commit>" daran.
            var plus = informational.IndexOf('+');
            var text = plus < 0 ? informational : informational[..plus];
            if (Version.TryParse(text, out var parsed))
                return $"{parsed.Major}.{parsed.Minor}.{Math.Max(parsed.Build, 0)}";
        }

        return assembly.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";
    }

    public sealed record UpdateInfo(string Version, string Url, string Notes, string? DownloadUrl, long Size)
    {
        /// Ohne Datei im Release bleibt nur der Weg ueber den Browser.
        public bool CanInstall => !string.IsNullOrEmpty(DownloadUrl);

        public string SizeText => Size > 0 ? $"{Size / 1024d / 1024d:0} MB" : "";
    }

    /// Null heisst: alles aktuell, oder es liess sich nichts feststellen.
    public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync(ReleaseApi, ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = Text(root, "tag_name");
            var name = Text(root, "name");
            var notes = Text(root, "body");
            var url = Text(root, "html_url");

            // Die Versionsnummer steht im Tag, im Namen oder im Text - der
            // erste brauchbare Fund zaehlt.
            var latest = FindVersion(tag) ?? FindVersion(name) ?? FindVersion(notes);
            if (latest is null) return null;

            if (!Version.TryParse(CurrentVersion, out var current)) return null;
            if (latest <= current) return null;

            // Die Programmdatei aus den Anhaengen des Releases
            string? download = null;
            long size = 0;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!Text(asset, "name").EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    download = Text(asset, "browser_download_url");
                    size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var n) ? n : 0;
                    break;
                }
            }

            return new UpdateInfo(
                $"{latest.Major}.{latest.Minor}.{latest.Build}",
                string.IsNullOrWhiteSpace(url) ? ReleasePage : url,
                notes.Trim(),
                download,
                size);
        }
        catch
        {
            // Ohne Netz oder bei einem Fehler bleibt es beim Schweigen.
            return null;
        }
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    /// Erste Zeichenfolge der Form 1.2.3 in einem Text.
    private static Version? FindVersion(string text)
    {
        var m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"(\d+)\.(\d+)\.(\d+)");
        return m.Success && Version.TryParse(m.Value, out var v) ? v : null;
    }

    /* ---------------- Selbstaustausch ---------------- */

    // Windows laesst eine laufende Exe nicht ueberschreiben - umbenennen aber
    // schon. Darauf beruht der Austausch:
    //
    //   1. neue Datei als M2Hub.update daneben legen
    //   2. die laufende Exe nach M2Hub.exe.old umbenennen
    //   3. die neue an ihren Platz schieben
    //   4. neu starten und beenden
    //   5. beim naechsten Start die .old-Datei loeschen
    //
    // Geht Schritt 3 schief, wird Schritt 2 zurueckgenommen - sonst stuende
    // das Programm ohne Exe da.

    private const string UpdateSuffix = ".update";
    private const string BackupSuffix = ".old";

    /// Pfad der laufenden Programmdatei, oder null wenn sie sich nicht
    /// bestimmen laesst (dann ist kein Austausch moeglich).
    public static string? ExecutablePath
    {
        get
        {
            var path = Environment.ProcessPath;
            return string.IsNullOrEmpty(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? null
                : path;
        }
    }

    /// Laesst sich die Exe an ihrem Platz ersetzen? In C:\Programme etwa nicht,
    /// dort fehlen ohne Adminrechte die Schreibrechte.
    public static bool CanReplaceInPlace()
    {
        var exe = ExecutablePath;
        if (exe is null) return false;

        try
        {
            var probe = Path.Combine(Path.GetDirectoryName(exe)!, $".m2hub-{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// Reste eines frueheren Austauschs entfernen. Beim ersten Start nach dem
    /// Update laeuft die alte Datei nicht mehr und laesst sich loeschen.
    public static void CleanupBackup()
    {
        var exe = ExecutablePath;
        if (exe is null) return;

        try
        {
            var backup = exe + BackupSuffix;
            if (File.Exists(backup)) File.Delete(backup);

            var staged = exe + UpdateSuffix;
            if (File.Exists(staged)) File.Delete(staged);
        }
        catch
        {
            // Noch gesperrt - beim naechsten Start erneut versuchen.
        }
    }

    /// Laedt die neue Programmdatei neben die laufende. Gibt den Pfad zurueck,
    /// oder null bei einem Fehler.
    public async Task<string?> DownloadAsync(
        UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var exe = ExecutablePath;
        if (exe is null || info.DownloadUrl is null) return null;

        var target = exe + UpdateSuffix;

        try
        {
            using var res = await _http
                .GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;

            var total = res.Content.Headers.ContentLength ?? info.Size;
            await using var source = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                done += read;
                if (total > 0) progress?.Report(done * 100d / total);
            }

            await file.FlushAsync(ct).ConfigureAwait(false);
            file.Close();

            // Eine abgebrochene Uebertragung faellt hier auf: eine Exe ist nie
            // nur ein paar Kilobyte gross.
            if (new FileInfo(target).Length < 1_000_000)
            {
                File.Delete(target);
                return null;
            }

            return target;
        }
        catch
        {
            try { if (File.Exists(target)) File.Delete(target); } catch { /* egal */ }
            return null;
        }
    }

    /// Tauscht die Dateien und startet das Programm neu. Bei Erfolg kehrt der
    /// Aufruf nicht sinnvoll zurueck - der Aufrufer beendet danach die App.
    public static bool ApplyAndRestart(string staged)
    {
        var exe = ExecutablePath;
        if (exe is null || !File.Exists(staged)) return false;

        var backup = exe + BackupSuffix;
        var renamed = false;

        try
        {
            if (File.Exists(backup)) File.Delete(backup);

            File.Move(exe, backup);
            renamed = true;

            File.Move(staged, exe);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe)
            {
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            // Der zweite Schritt ist schiefgegangen - die laufende Exe wieder
            // an ihren Platz holen, sonst ist das Programm nach dem Beenden weg.
            if (renamed)
            {
                try
                {
                    if (!File.Exists(exe)) File.Move(backup, exe);
                }
                catch
                {
                    // Mehr laesst sich hier nicht tun.
                }
            }
            return false;
        }
    }
}
