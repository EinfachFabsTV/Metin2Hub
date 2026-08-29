using System.Text.RegularExpressions;
using M2Hub.Desktop.Models;

namespace M2Hub.Desktop.Services.Forum;

/// Portierung von shared/utils/scraper/events.ts - der Eventkalender-Thread
/// (thread/90381-eventkalender) mit je einem Beitrag pro Server.
public static partial class EventCalendar
{
    /// Servernamen, die in den Kalendern der verschiedenen Foren vorkommen.
    /// Der erste Treffer im Beitrag bestimmt die Beschriftung des Reiters.
    ///
    /// Chimera, Oceana und Blos teilen sich einen Kalender - das Forum schreibt
    /// fuer alle drei einen Beitrag. Fuer die Accounts sind es trotzdem drei
    /// Server; diese Aufteilung steht in ServerCatalog.
    private static readonly (string Needle, string Label)[] KnownServers =
    [
        ("chimera", "[Ruby]Chimera / [SAPPHIRE]Oceana / [DIAMOND]Blos"),
        ("oceana", "[Ruby]Chimera / [SAPPHIRE]Oceana / [DIAMOND]Blos"),
        ("blos", "[Ruby]Chimera / [SAPPHIRE]Oceana / [DIAMOND]Blos"),
        ("tigerghost", "Tigerghost"),
        ("italia", "Italia"),
        ("marmara", "Marmara"),
        ("bagjanamu", "Bağjanamu"),
    ];

    private static readonly string[] Weekdays =
        ["montag", "dienstag", "mittwoch", "donnerstag", "freitag", "samstag", "sonntag"];

    [GeneratedRegex("<div class=\"messageText\"[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MessageText();

    [GeneratedRegex("<footer class=\"messageFooter\"|<div class=\"messageText\"", RegexOptions.IgnoreCase)]
    private static partial Regex PostEnd();

    [GeneratedRegex(@"<table[\s\S]*?</table>", RegexOptions.IgnoreCase)]
    private static partial Regex Table();

    [GeneratedRegex(@"<tr[\s\S]*?</tr>", RegexOptions.IgnoreCase)]
    private static partial Regex TableRow();

    [GeneratedRegex(@"<t[dh][\s\S]*?</t[dh]>", RegexOptions.IgnoreCase)]
    private static partial Regex TableCell();

    [GeneratedRegex(@"(\d{1,2})(?:[:.]\d{2})?\s*[-–—]\s*(\d{1,2})(?:[:.]\d{2})?")]
    private static partial Regex TimeRange();

    [GeneratedRegex(@"^\s*(\d{1,2})\.\s*")]
    private static partial Regex DayPrefix();

    [GeneratedRegex(@"<u(?![a-z])[^>]*>([\s\S]*?)</u>", RegexOptions.IgnoreCase)]
    private static partial Regex Underlined();

    [GeneratedRegex(@"<li[\s\S]*?</li>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"<[uo]l[\s\S]*?</[uo]l>", RegexOptions.IgnoreCase)]
    private static partial Regex AnyList();

    [GeneratedRegex(@"<(?:p|div)[^>]*>[\s\S]*?</(?:p|div)>", RegexOptions.IgnoreCase)]
    private static partial Regex Block();

    [GeneratedRegex(@"<br\s*/?\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"\d{1,2}\.")]
    private static partial Regex HasDayNumber();

    /* ---------- Beitraege trennen ---------- */

    public static List<string> SplitPosts(string html)
    {
        var src = Html.DecodeEntities(html ?? "");
        var posts = new List<string>();

        foreach (Match m in MessageText().Matches(src))
        {
            var start = m.Index + m.Length;
            var rest = src[start..];
            var end = PostEnd().Match(rest);
            posts.Add(end.Success ? rest[..end.Index] : rest);
        }

        // Ohne Forum-Markup gilt das ganze Dokument als ein Beitrag
        return posts.Count > 0 ? posts : [src];
    }

    /// Servername aus einem Beitrag, sofern einer der bekannten Namen darin
    /// vorkommt. Sonst null - dann traegt der Thread-Titel die Beschriftung.
    public static string? DetectServer(string postHtml)
    {
        var low = Fold(Html.HtmlToText(postHtml));
        foreach (var (needle, label) in KnownServers)
            if (low.Contains(needle, StringComparison.Ordinal)) return label;
        return null;
    }

    /// Vergleichsform: klein, ohne Umlaute und diakritische Zeichen. Damit
    /// trifft „Bağjanamu" auch als „bagjanamu".
    private static string Fold(string text)
    {
        var lower = text.ToLowerInvariant()
            .Replace("ä", "a").Replace("ö", "o").Replace("ü", "u").Replace("ß", "ss")
            .Replace("ğ", "g").Replace("ı", "i").Replace("ş", "s").Replace("ç", "c")
            .Replace("à", "a").Replace("è", "e").Replace("é", "e").Replace("ì", "i")
            .Replace("ò", "o").Replace("ù", "u");
        return lower;
    }

    /// Aus einer Beschriftung eine stabile Kennung machen - sie steht in den
    /// Einstellungen und darf sich nicht mit der Sprache aendern.
    public static string Slug(string label)
    {
        var folded = Fold(label);
        var sb = new System.Text.StringBuilder(folded.Length);
        foreach (var c in folded)
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-').Replace("--", "-");
    }

    /// "Event 1 (16:00-20:00)" → 16-20 · "20:00 -0:00" → 20-24
    public static TimeColDto ParseTimeCol(string label)
    {
        var text = Html.HtmlToText(label);
        var m = TimeRange().Match(text);
        if (!m.Success) return new TimeColDto { Label = text, From = 0, To = 24 };

        var from = int.Parse(m.Groups[1].Value);
        var to = int.Parse(m.Groups[2].Value);
        if (to == 0 || to <= from) to = 24;
        return new TimeColDto { Label = text, From = from, To = to };
    }

    public sealed record CalendarTable(string Type, List<TimeColDto> Columns, List<CalRowDto> Rows);

    public static CalendarTable? ParseCalendarTable(string postHtml)
    {
        var table = Table().Match(postHtml ?? "");
        if (!table.Success) return null;

        var columns = new List<TimeColDto>();
        var rows = new List<CalRowDto>();
        var type = "date";
        CalRowDto? pending = null;

        foreach (Match tr in TableRow().Matches(table.Value))
        {
            var filled = TableCell().Matches(tr.Value)
                .Select(td => Html.HtmlToText(td.Value))
                .Where(c => c.Length > 0)
                .ToList();
            if (filled.Count == 0) continue;

            var first = filled[0];
            var dayMatch = DayPrefix().Match(first);
            var wdIdx = Array.IndexOf(Weekdays, first.ToLowerInvariant().Trim());

            // Kopfzeile: erste Zelle ist "Datum"/"Tag", also keine Datenzeile
            if (columns.Count == 0 && !dayMatch.Success && wdIdx < 0)
            {
                columns = filled.Skip(1).Select(ParseTimeCol).ToList();
                continue;
            }

            if (dayMatch.Success && filled.Count >= 2)
            {
                type = "date";
                rows.Add(new CalRowDto
                {
                    Label = first,
                    D = int.Parse(dayMatch.Groups[1].Value),
                    Cells = filled.Skip(1).ToList(),
                });
                pending = null;
                continue;
            }

            if (wdIdx >= 0 && filled.Count >= 2)
            {
                type = "weekday";
                rows.Add(new CalRowDto
                {
                    Label = first,
                    Weekday = wdIdx + 1,
                    Cells = filled.Skip(1).ToList(),
                });
                pending = null;
                continue;
            }

            // Zweizeilige Form: erst "03. Juli", die Events folgen in der naechsten Zeile
            if (filled.Count == 1 && (dayMatch.Success || wdIdx >= 0))
            {
                pending = dayMatch.Success
                    ? new CalRowDto { Label = first, D = int.Parse(dayMatch.Groups[1].Value) }
                    : new CalRowDto { Label = first, Weekday = wdIdx + 1 };
                continue;
            }

            if (pending is not null)
            {
                type = pending.D is not null ? "date" : "weekday";
                pending.Cells = filled;
                rows.Add(pending);
                pending = null;
            }
        }

        if (rows.Count == 0) return null;

        // Ohne brauchbare Kopfzeile die Spaltenzahl aus den Daten ableiten
        if (columns.Count == 0)
        {
            var n = rows.Max(r => r.Cells.Count);
            columns = Enumerable.Range(1, n)
                .Select(i => new TimeColDto { Label = $"Event {i}", From = 0, To = 24 })
                .ToList();
        }

        return new CalendarTable(type, columns, rows);
    }

    /// "zusaetzliche Events" eines Beitrags - nur Zeilen des gesuchten Monats
    /// mit Tagesangabe.
    public static List<string> ParseSpecials(string postHtml, int month)
    {
        var html = postHtml ?? "";
        var head = Underlined().Matches(html).FirstOrDefault(h =>
        {
            var t = Html.HtmlToText(h.Groups[1].Value).ToLowerInvariant();
            return t.Contains("event") && (t.Contains("zus") || t.Contains("weitere"));
        });

        // Ohne Ueberschrift den ganzen Beitrag ohne Tabelle durchsuchen
        var section = head is not null
            ? html[(head.Index + head.Length)..]
            : Table().Replace(html, " ");

        var lines = new List<string>();
        foreach (Match li in ListItem().Matches(section))
        {
            var t = Html.HtmlToText(li.Value);
            if (t.Length > 0) lines.Add(t);
        }

        var withoutLists = AnyList().Replace(section, " ");
        foreach (Match p in Block().Matches(withoutLists))
        {
            var withBreaks = LineBreak().Replace(p.Value, " __BR__ ");
            foreach (var ln in Html.HtmlToText(withBreaks).Split("__BR__"))
            {
                var t = ln.Trim();
                if (t.Length > 0) lines.Add(t);
            }
        }

        var monLow = Html.MonthDeName(month).ToLowerInvariant();
        var seen = new HashSet<string>();
        var result = new List<string>();
        foreach (var sp in lines)
        {
            if (!sp.ToLowerInvariant().Contains(monLow)) continue;
            if (!HasDayNumber().IsMatch(sp)) continue;
            var key = NormLine(sp);
            if (key.Length == 0 || !seen.Add(key)) continue;
            result.Add(sp);
        }
        return result;
    }

    private static string NormLine(string s) =>
        Html.HtmlToText(s).Replace('–', '-').Replace('—', '-').Trim(' ', '-').ToLowerInvariant();

    public static bool TableMatchesMonth(List<CalRowDto> rows, int month)
    {
        var name = Html.MonthDeName(month).ToLowerInvariant();
        if (name.Length == 0) return false;
        return rows.Any(r => r.Label.ToLowerInvariant().Contains(name));
    }

    /// Kalender eines Threads.
    ///
    /// Im deutschen Forum steht in einem Thread je Server ein Beitrag; in den
    /// anderen Foren gibt es je Server einen eigenen Thread. Beides deckt diese
    /// Funktion ab: Findet sich im Beitrag ein bekannter Servername, gilt der;
    /// sonst traegt der Thread-Titel die Beschriftung.
    ///
    /// Je Server gibt es im deutschen Thread mehrere Beitraege - fuer jeden
    /// Monat einen. Genommen wird der, dessen Tabelle den gesuchten Monat
    /// nennt; gibt es keinen solchen, gilt der zuletzt geschriebene.
    public static List<ServerCalDto> ParseEventPost(string html, int month, string fallbackLabel = "")
    {
        var found = new Dictionary<string, (ServerCalDto Cal, bool Exact)>(StringComparer.Ordinal);
        var withoutName = 0;

        foreach (var post in SplitPosts(html))
        {
            var table = ParseCalendarTable(post);
            if (table is null) continue;

            var label = DetectServer(post);
            if (label is null)
            {
                // Kein Servername im Beitrag - dann steht er im Thread-Titel.
                withoutName++;
                label = fallbackLabel.Length > 0
                    ? (withoutName > 1 ? $"{fallbackLabel} {withoutName}" : fallbackLabel)
                    : $"Kalender {withoutName}";
            }

            var key = Slug(label);
            var cal = new ServerCalDto
            {
                Key = key,
                Label = label,
                Type = table.Type,
                Columns = table.Columns,
                Rows = table.Rows,
                Specials = ParseSpecials(post, month),
            };
            var exact = table.Type == "date" && TableMatchesMonth(table.Rows, month);

            // Ein Monatstreffer schlaegt alles; sonst gewinnt der spaetere Beitrag
            if (!found.TryGetValue(key, out var prev) || (exact && !prev.Exact) || (!prev.Exact && !exact))
                found[key] = (cal, exact);
        }

        return found.Values.Select(v => v.Cal).ToList();
    }

    /// Was laeuft bei diesem Server gerade?
    public static CurrentDto? CurrentFor(ServerCalDto cal, Html.BerlinMoment now)
    {
        var row = cal.Type == "date"
            ? cal.Rows.FirstOrDefault(r => r.D == now.D)
            : cal.Rows.FirstOrDefault(r => r.Weekday == now.Weekday);
        if (row is null) return null;

        var idx = cal.Columns.FindIndex(c => now.Hour >= c.From && now.Hour < c.To);
        if (idx < 0) return null;

        var text = idx < row.Cells.Count ? row.Cells[idx] : "";
        if (string.IsNullOrWhiteSpace(text)) return null;

        var col = cal.Columns[idx];
        return new CurrentDto { Text = text, From = col.From, To = col.To };
    }
}
