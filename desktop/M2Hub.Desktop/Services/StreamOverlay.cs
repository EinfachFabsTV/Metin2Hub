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
///   ziel.txt       dasselbe fuer die zweite Karte: Titel, Zielbetrag, Netto,
///   ziel-wert.txt  Offenes, Ausgaben, Fortschritt, Truhen, Runs, der aktive
///   ziel-netto.txt Lauf und die geschaetzten Restlaeufe. Jede Angabe, die in
///   ...            `goal.html` steht, steht auch als eigene Datei da - wer
///                  die Karte selbst bauen will, soll nichts nachrechnen
///                  muessen.
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
        // Die beiden Karten sind unabhaengig: wer nur das Ziel zeigt, hat die
        // erste aus - und umgekehrt.
        try
        {
            if (!store.Settings.Stream.Enabled && !store.Settings.Stream.GoalEnabled) return;

            System.IO.Directory.CreateDirectory(Directory);

            if (store.Settings.Stream.Enabled)
            {
                var view = Snapshot();

                WriteFile("schulden.txt", view.Debt);
                WriteFile("offen.txt", view.Rest);
                WriteFile("fortschritt.txt", view.Progress);
                WriteFile("truhen.txt", view.Chests);
                WriteFile("runs.txt", view.Runs);
                WriteFile("overlay.html", Page(view));
            }

            WriteGoal();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// Die zweite Karte: das aktive Ziel. Eigene Datei, eigene Quelle in OBS -
    /// wer nur das Ziel zeigen will, nimmt nur sie.
    private void WriteGoal()
    {
        var s = store.Settings.Stream;
        if (!s.GoalEnabled) return;

        var goal = store.Goals.Goals.Where(g => !g.Done).OrderBy(g => g.Sort).FirstOrDefault();
        if (goal is null) return;

        var runProfit = store.Runs.Entries
            .Where(e => e.AddedAt >= goal.StartedAt)
            .Sum(e => e.Chests * PriceOf(e)) / Money.KkPerW;

        // Dieselbe Aufteilung wie im Bereich Goals: der Shop-Bestand ist
        // weder Ausgabe noch volle Einnahme - von ihm zaehlt nur, was nicht
        // ueber die Laeufe schon im Gewinn steht.
        var income = goal.Bookings.Where(b => b.Kind == "income").Sum(b => b.Amount)
            + goal.Bookings.Where(b => b.Kind == "shop").Sum(ShopNet);
        var expenses = goal.Bookings
            .Where(b => b.Kind != "income" && b.Kind != "shop")
            .Sum(b => b.Amount);
        var net = GoalCalc.Net(runProfit, income, expenses, goal.CarryIn);
        var rest = GoalCalc.Rest(goal.Target, net);
        var progress = GoalCalc.Progress(goal.Target, net);

        var entries = InScope().ToList();
        var run = RunCatalog.Find(goal.ActiveRun)?.Name ?? goal.ActiveRun;

        WriteFile("ziel.txt", goal.Title.Length > 0 ? goal.Title : Won(goal.Target));
        WriteFile("ziel-wert.txt", Won(goal.Target));
        WriteFile("ziel-netto.txt", Won(net));
        WriteFile("ziel-offen.txt", Won(rest));
        WriteFile("ziel-ausgaben.txt", Won(expenses));
        WriteFile("ziel-einnahmen.txt", Won(income));
        WriteFile("ziel-fortschritt.txt", progress.ToString("0.#", CultureInfo.CurrentCulture) + " %");

        // Truhen und Runs im eingestellten Zeitraum. Sie stehen auch in
        // truhen.txt und runs.txt - aber nur, wenn die erste Karte an ist;
        // wer allein das Ziel zeigt, haette sie sonst nicht.
        WriteFile("ziel-truhen.txt", entries.Sum(e => e.Chests).ToString("N0", CultureInfo.CurrentCulture));
        WriteFile("ziel-runs.txt", entries.Count.ToString("N0", CultureInfo.CurrentCulture));

        WriteFile("ziel-lauf.txt", run);
        // „Noch ungefaehr X Laeufe" - geschaetzt aus dem, was der aktive Lauf
        // bisher im Mittel einbrachte. Ohne Eintraege dazu bleibt es leer, und
        // die Karte zeigt stattdessen, was offen ist.
        var perRun = PerRun(goal.ActiveRun);
        var runsLeft = perRun > 0m && rest > 0m
            ? Loc.T("goals.runsLeft", GoalCalc.Runs(rest, perRun).ToString("N0", CultureInfo.CurrentCulture))
            : "";

        // „Noch ungefaehr X Laeufe" als blanke Zahl daneben - fuer eine
        // Text-Quelle, die ihre Beschriftung selbst mitbringt.
        WriteFile("ziel-laeufe.txt", runsLeft);
        WriteFile("ziel-laeufe-zahl.txt", perRun > 0m && rest > 0m
            ? GoalCalc.Runs(rest, perRun).ToString("N0", CultureInfo.CurrentCulture)
            : "");

        WriteFile("goal.html", GoalPage(
            goal.Title.Length > 0 ? goal.Title : Loc.T("stream.goal.title"),
            Won(goal.Target), Won(net), Won(rest), expenses > 0m ? Won(expenses) : "",
            progress, entries.Sum(e => e.Chests), entries.Count, run, runsLeft));
    }

    /// Was ein Shop-Posten beitraegt: der Betrag ohne die Truhen, die ueber
    /// die Laeufe schon gezaehlt sind.
    private decimal ShopNet(GoalBookingDto booking)
    {
        var counted = booking.Chests.Sum(c => c.Chests * ChestPrice(c)) / Money.KkPerW;

        return GoalCalc.ShopNet(booking.Amount, counted);
    }

    /// Der Preis einer Truhenzeile in kk: ihr eigener, sonst der des Laufs.
    private decimal ChestPrice(GoalChestDto chest)
    {
        if (chest.Price > 0m) return chest.Price;

        return store.Runs.ChestPrice.TryGetValue(chest.Run, out var p)
            ? p
            : RunCatalog.DefaultChestPrice;
    }

    /// Was ein Lauf dieser Art im Mittel einbringt, in Won.
    private decimal PerRun(string runKey)
    {
        var mine = store.Runs.Entries.Where(e => e.Run == runKey).ToList();
        if (mine.Count == 0) return 0m;

        return mine.Sum(e => e.Chests * PriceOf(e)) / mine.Count / Money.KkPerW;
    }

    private decimal PriceOf(RunEntryDto entry)
    {
        if (entry.Price > 0m) return entry.Price;

        return store.Runs.ChestPrice.TryGetValue(entry.Run, out var p)
            ? p
            : RunCatalog.DefaultChestPrice;
    }

    /// Die Karte des Ziels. Jedes Stueck steht nur da, wenn es eingeschaltet
    /// ist - in einem Overlay ist Platz das Knappste.
    /// Die Karte des Ziels - **eine Zeile**, kein Stapel.
    ///
    /// Gestapelt wurde sie so hoch, dass man sie im Stream kleinziehen musste,
    /// und dann war sie nicht mehr zu lesen. Jetzt steht alles nebeneinander:
    /// Titel, Balken, und dahinter die eingeschalteten Angaben, durch Punkte
    /// getrennt. Was fehlt, laesst eine Luecke statt einer leeren Zeile.
    ///
    /// Die Schriftgroesse steht in den Einstellungen, damit man die Karte
    /// nicht in OBS skalieren muss - skaliert wird sonst auch die Unschaerfe.
    private string GoalPage(
        string title, string target, string net, string rest, string expenses,
        double progress, int chests, int runs, string run, string runsLeft)
    {
        var s = store.Settings.Stream;
        var parts = new List<string>();

        if (s.GoalShowProgress)
            parts.Add($"""<span class="big">{Html(net)}</span><span class="of">/ {Html(target)}</span>""");

        if (s.GoalShowNet && expenses.Length > 0)
            parts.Add($"""<span class="warn">− {Html(expenses)}</span>""");

        if (s.GoalShowRuns && runsLeft.Length > 0)
            parts.Add($"""<span>{Html(runsLeft)}</span>""");

        if (s.GoalShowRuns && runsLeft.Length == 0)
            parts.Add($"""<span>{Html(rest)}</span>""");

        if (s.GoalShowChests)
            parts.Add($"""<span>{chests} ⬦ {runs}</span>""");

        if (s.GoalShowActiveRun)
            parts.Add($"""<span class="run">{Html(run)}</span>""");

        var line = string.Join("""<span class="dot">·</span>""", parts);
        var bar = s.GoalShowProgress
            ? $"""
                <div class="bar"><div class="fill" style="width: {progress.ToString("0.#", CultureInfo.InvariantCulture)}%"></div></div>
            """
            : "";

        return Frame(title, line, bar, Size(s.GoalSize));
    }

    /// Drei Stufen, damit man die Karte nicht in OBS kleinziehen muss.
    /// Die Deckkraft des Kartenhintergrunds, als CSS-Anteil („0.82").
    /// Der Rand geht mit: ein deutlicher Rahmen um nichts sieht aus wie ein
    /// Fehler.
    private double Opacity => Math.Clamp(store.Settings.Stream.Opacity, 0, 100) / 100.0;

    private string Alpha => Opacity.ToString("0.##", CultureInfo.InvariantCulture);

    private string BorderAlpha => (Opacity * 0.1).ToString("0.###", CultureInfo.InvariantCulture);

    private static string Size(string? key) => key switch
    {
        "s" => "13",
        "l" => "19",
        _ => "16",
    };

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
    private string Page(View view) => $$"""
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
            background: rgba(11, 17, 31, {{Alpha}});
            border: 1px solid rgba(255, 255, 255, {{BorderAlpha}});
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

    /// Der Rahmen der Goal-Karte: eine flache Leiste, dieselben Farben wie
    /// die erste Karte.
    private string Frame(string title, string line, string bar, string size) => $$"""
        <!doctype html>
        <html lang="de">
        <head>
        <meta charset="utf-8">
        <title>M2Hub</title>
        <style>
          html, body { margin: 0; background: transparent; }
          body {
            font-family: "Segoe UI", system-ui, sans-serif;
            color: #F9FAFB; font-variant-numeric: tabular-nums;
            font-size: {{size}}px;
          }
          .card {
            display: inline-flex; align-items: center; gap: .75em;
            background: rgba(11, 17, 31, {{Alpha}});
            border: 1px solid rgba(255, 255, 255, {{BorderAlpha}});
            border-radius: 999px; padding: .5em 1.1em;
            white-space: nowrap;
          }
          .title { font-weight: 700; }
          .bar {
            width: 7em; height: .45em; border-radius: 999px;
            background: rgba(255, 255, 255, .1); overflow: hidden;
          }
          .fill { height: 100%; background: #10B981; border-radius: 999px; }
          .line { display: inline-flex; align-items: baseline; gap: .45em; }
          .big { font-weight: 700; color: #10B981; }
          .of { color: #9CA3AF; font-size: .85em; }
          .warn { color: #F59E0B; }
          .run { color: #3B82F6; }
          .dot { color: #4B5563; }
        </style>
        </head>
        <body>
          <div class="card">
            <span class="title">{{Html(title)}}</span>
        {{bar}}    <span class="line">{{line}}</span>
          </div>
          <script>
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
