using System.Text;
using Avalonia.Platform;

namespace M2Hub.Desktop.Services;

/// Die Patchnotes, wie sie in den Einstellungen stehen.
///
/// Die Quelle ist `desktop/CHANGELOG.md`; eine Kopie liegt als
/// `Assets/CHANGELOG.md` bei und wird mit der Exe ausgeliefert, damit die App
/// sie ohne Netz zeigen kann.
///
/// Gelesen wird nur - geschrieben wird die Datei von Hand, bei jeder
/// Veroeffentlichung.
public static class Patchnotes
{
    private const string Asset = "avares://M2Hub/Assets/CHANGELOG.md";

    /// Der Abschnitt der neuesten Version, ohne die Kopfzeile der Datei.
    /// Faellt das Lesen aus, bleibt der Text leer und die Karte verschwindet -
    /// Patchnotes sind nichts, wofuer die App stehenbleiben darf.
    public static string Latest()
    {
        var all = All();
        if (all.Length == 0) return "";

        var start = all.IndexOf("\n## ", StringComparison.Ordinal);
        if (start < 0) return "";

        var next = all.IndexOf("\n## ", start + 4, StringComparison.Ordinal);
        var text = next < 0 ? all[start..] : all[start..next];

        return Clean(text);
    }

    /// Derselbe Abschnitt, aber ohne die Versionszeile: im Fenster steht die
    /// Version schon in der Kopfzeile, zweimal untereinander waere sie Beiwerk.
    public static string LatestBody()
    {
        var text = Latest();
        var cut = text.IndexOf('\n');

        return cut < 0 ? "" : text[(cut + 1)..].Trim();
    }

    /// Alles, von der neuesten Version abwaerts.
    public static string Everything()
    {
        var all = All();
        var start = all.IndexOf("\n## ", StringComparison.Ordinal);

        return start < 0 ? Clean(all) : Clean(all[start..]);
    }

    private static string All()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri(Asset));
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd().Replace("\r\n", "\n");
        }
        catch (FileNotFoundException) { return ""; }
        catch (IOException) { return ""; }
    }

    /// Markdown gibt es in der App nicht - die Sternchen und Bindestriche
    /// werden zu etwas, das sich als Text lesen laesst.
    private static string Clean(string text)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd());
        var build = new StringBuilder();

        foreach (var line in lines)
        {
            var clean = line
                .Replace("**", "")
                .Replace("`", "");

            if (clean.StartsWith("## ", StringComparison.Ordinal))
                clean = clean[3..];
            else if (clean.StartsWith("- ", StringComparison.Ordinal))
                clean = "  • " + clean[2..];
            else if (clean.StartsWith("  ", StringComparison.Ordinal) && clean.Trim().Length > 0)
                clean = "   " + clean.Trim();

            build.AppendLine(clean);
        }

        return build.ToString().Trim();
    }
}
