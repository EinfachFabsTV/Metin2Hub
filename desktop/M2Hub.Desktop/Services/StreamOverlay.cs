using System.Globalization;
using System.Text;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.Services;

/// Schreibt die Zahlen fuer eine Stream-Einblendung in Dateien.
///
/// **Kein Server.** Die App stellt keinen Port auf, oeffnet keine Firewall und
/// spricht mit niemandem - sie legt Dateien in `%AppData%\M2Hub\stream\` ab,
/// und OBS liest sie von der Platte:
///
///   overlay.html   eine fertige Karte im Aussehen der App, fuer eine
///                  Browser-Quelle mit durchsichtigem Hintergrund. Sie laedt
///                  sich alle paar Sekunden selbst neu und holt sich so die
///                  neuen Zahlen; eine Browser-Quelle merkt von sich aus
///                  nicht, dass die Datei sich geaendert hat.
///   schulden.txt   die blanken Zahlen, je eine Datei - fuer „Text (GDI+)"
///   offen.txt      mit „aus Datei lesen", wenn man es selbst gestalten will.
///   fortschritt.txt
///   truhen.txt
///   runs.txt
///
/// Geschrieben wird bei jeder Aenderung (Lauf eingetragen, Schuld angepasst)
/// und beim Umschalten des Zeitraums, nicht in einem Takt - sonst haengt die
/// Einblendung der Eingabe hinterher.
///
/// Faellt das Schreiben aus (Ordner weg, Datei gesperrt, weil OBS gerade
/// liest), bleibt die alte Datei stehen und die App laeuft weiter. Eine
/// Einblendung ist nichts, wofuer ein Programm stehenbleiben darf.
public sealed class StreamOverlay(LocalStore store)
{
    private static string Directory => Path.Combine(LocalStore.Directory, "stream");

    public static string FolderPath => Directory;

    /// Die drei Zeitraeume, zwischen denen umgeschaltet wird.
    public const string Today = "today";
    public const string Session = "session";
    public const string Total = "total";

    /// Alles neu schreiben. Ist die Einblendung aus, passiert nichts - die
    /// Dateien bleiben stehen, wie sie zuletzt waren.
    public void Write()
    {
        if (!store.Settings.Stream.Enabled) return;

        try
        {
            System.IO.Directory.CreateDirectory(Directory);

            var view = Snapshot();

            WriteFile("schulden.txt", view.Debt);
            WriteFile("offen.txt", view.Rest);
            WriteFile("fortschritt.txt", view.Progress);
            WriteFile("truhen.txt", view.Chests);
            WriteFile("runs.txt", view.Runs);
            WriteFile("overlay.html", Page(view));
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// Die Zahlen, wie sie in der Einblendung stehen.
    public sealed record View(
        string Debt, string Rest, string Progress, double ProgressValue,
        string Chests, string Runs, string Scope);

    public View Snapshot()
    {
        var debt = store.Settings.Debt;
        var rest = DebtCalc.Rest(debt.Total, debt.Farmed);
        var progress = debt.Total <= 0m
            ? 0
            : (double)Math.Clamp(debt.Farmed / debt.Total * 100m, 0m, 100m);

        var entries = InScope().ToList();

        return new View(
            Won(debt.Total),
            Won(rest),
            progress.ToString("0.#", CultureInfo.CurrentCulture) + " %",
            progress,
            entries.Sum(e => e.Chests).ToString("N0", CultureInfo.CurrentCulture),
            entries.Count.ToString("N0", CultureInfo.CurrentCulture),
            Loc.T("stream.scope." + store.Settings.Stream.Scope));
    }

    /// Welche Eintraege zaehlen - ueber alle Laeufe, denn im Stream interessiert
    /// der Abend, nicht der einzelne Lauf.
    private IEnumerable<RunEntryDto> InScope()
    {
        var all = store.Runs.Entries;

        return store.Settings.Stream.Scope switch
        {
            Today => all.Where(e => e.Day == DateTime.Today.ToString("yyyy-MM-dd")),
            Session => all.Where(e => e.AddedAt >= store.Settings.Stream.SessionStart),
            _ => all,
        };
    }

    private static string Won(decimal value) =>
        value.ToString("#,0.##", CultureInfo.CurrentCulture) + " " + Loc.T("stream.won");

    private static void WriteFile(string name, string content) =>
        File.WriteAllText(Path.Combine(Directory, name), content, Encoding.UTF8);

    /// Die Karte als HTML. Farben aus `Styles/Theme.axaml` - die eine Quelle
    /// der Wahrheit steht dort, hier stehen dieselben Werte, weil eine
    /// Browser-Quelle kein XAML liest.
    private static string Page(View view) => $$"""
        <!doctype html>
        <html lang="de">
        <head>
        <meta charset="utf-8">
        <title>M2Hub</title>
        <style>
          /* Durchsichtig, damit in OBS nur die Karte steht. */
          html, body { margin: 0; background: transparent; }
          body {
            font-family: "Segoe UI", system-ui, sans-serif;
            color: #F9FAFB;
            /* Tabellenziffern: beim Hochzaehlen springt nichts. */
            font-variant-numeric: tabular-nums;
          }
          .card {
            display: inline-block;
            background: rgba(11, 17, 31, .82);
            border: 1px solid rgba(255, 255, 255, .08);
            border-radius: 14px;
            padding: 14px 18px;
            min-width: 320px;
          }
          .row { display: flex; gap: 26px; align-items: flex-end; }
          .label {
            font-size: 11px; letter-spacing: .06em; text-transform: uppercase;
            color: #9CA3AF; margin-bottom: 2px;
          }
          .value { font-size: 26px; font-weight: 700; line-height: 1; }
          .debt { color: #F59E0B; }
          .bar {
            margin-top: 12px; height: 8px; border-radius: 999px;
            background: rgba(255, 255, 255, .08); overflow: hidden;
          }
          .fill { height: 100%; background: #10B981; border-radius: 999px; }
          .foot {
            margin-top: 6px; display: flex; justify-content: space-between;
            font-size: 11px; color: #9CA3AF;
          }
        </style>
        </head>
        <body>
          <div class="card">
            <div class="row">
              <div>
                <div class="label">{{Html(Loc.T("stream.label.rest"))}}</div>
                <div class="value debt">{{Html(view.Rest)}}</div>
              </div>
              <div>
                <div class="label">{{Html(Loc.T("stream.label.chests"))}}</div>
                <div class="value">{{Html(view.Chests)}}</div>
              </div>
              <div>
                <div class="label">{{Html(Loc.T("stream.label.runs"))}}</div>
                <div class="value">{{Html(view.Runs)}}</div>
              </div>
            </div>
            <div class="bar"><div class="fill" style="width: {{view.ProgressValue.ToString("0.#", CultureInfo.InvariantCulture)}}%"></div></div>
            <div class="foot">
              <span>{{Html(view.Scope)}}</span>
              <span>{{Html(view.Progress)}}</span>
            </div>
          </div>
          <script>
            // Eine Browser-Quelle merkt nicht, dass die Datei sich geaendert
            // hat - also sieht die Seite selbst nach.
            setTimeout(function () { location.reload(); }, 5000);
          </script>
        </body>
        </html>
        """;

    /// Die Zahlen kommen aus der eigenen Eingabe, trotzdem wird nichts roh in
    /// die Seite geschrieben.
    private static string Html(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
