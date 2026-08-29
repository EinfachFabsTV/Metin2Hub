using System.Text.RegularExpressions;

namespace M2Hub.Desktop.Services.Forum;

/// Portierung von shared/utils/scraper/itemshop.ts und global-events.ts.
/// Beide Boards haben dasselbe Woltlab-Markup, deshalb teilen sie sich
/// Uebersicht, Beitrag und Zeitraum.
public static partial class Board
{
    public sealed record BoardThread(string ThreadId, string Url, string Title);

    public sealed record ParsedPost(string Title, string? ImageUrl, string BodyText, DateTime? PostedAt);

    public sealed record Period(DateTime? StartsAt, DateTime? EndsAt, bool AllDay);

    [GeneratedRegex(@"<a\b[^>]*href=""([^""]*\?thread/(\d+)-[^""]*)""[^>]*class=""[^""]*wbbTopicLink[^""]*""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex TopicLink();

    [GeneratedRegex(@"<a\b[^>]*class=""[^""]*wbbTopicLink[^""]*""[^>]*href=""([^""]*\?thread/(\d+)-[^""]*)""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex TopicLinkAlt();

    [GeneratedRegex(@"^\s*FAQ\b", RegexOptions.IgnoreCase)]
    private static partial Regex SkipTitle();

    [GeneratedRegex(@"<div[^>]*class=""[^""]*\bmessageText\b[^""]*""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MessageBodyStart();

    [GeneratedRegex(@"<div\b|</div>", RegexOptions.IgnoreCase)]
    private static partial Regex DivBoundary();

    /// Inhalt des Beitrags-Divs, verschachtelte Divs mitgezaehlt.
    ///
    /// Frueher stand hier ein `</div>`-Suchmuster ohne Zaehlung. Das brach beim
    /// ersten inneren Div ab - bei Beitraegen mit Inhaltsverzeichnis blieb vom
    /// Text nur „Inhaltsverzeichnis [ Verbergen ]" uebrig, und damit fand der
    /// Zeitraum-Parser nichts mehr („Zeitraum unbekannt").
    private static string? MessageBody(string html)
    {
        var start = MessageBodyStart().Match(html);
        if (!start.Success) return null;

        var from = start.Index + start.Length;
        var depth = 1;

        foreach (Match m in DivBoundary().Matches(html, from))
        {
            if (m.Value.StartsWith("</", StringComparison.Ordinal))
            {
                if (--depth == 0) return html[from..m.Index];
            }
            else
            {
                depth++;
            }
        }

        // Kein schliessendes Div gefunden - dann den Rest nehmen.
        return html[from..];
    }

    [GeneratedRegex(@"<img\b[^>]*\bsrc=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex FirstImage();

    [GeneratedRegex(@"<h1[^>]*>([\s\S]*?)</h1>", RegexOptions.IgnoreCase)]
    private static partial Regex Headline();

    [GeneratedRegex(@"<span[^>]*class=""[^""]*badge[^""]*""[^>]*>[\s\S]*?</span>", RegexOptions.IgnoreCase)]
    private static partial Regex Badge();

    [GeneratedRegex(@"<time\b[^>]*datetime=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PostTime();

    [GeneratedRegex(@"^https?://", RegexOptions.IgnoreCase)]
    private static partial Regex HttpUrl();

    /* ---------------- Board-Uebersicht ---------------- */

    /// Threads der Uebersicht. Angepinnte FAQ-Threads sind keine Aktionen.
    public static List<BoardThread> ParseBoardList(string html)
    {
        var outList = new List<BoardThread>();
        var seen = new HashSet<string>();

        foreach (var rx in new[] { TopicLink(), TopicLinkAlt() })
        {
            foreach (Match m in rx.Matches(html ?? ""))
            {
                var threadId = m.Groups[2].Value;
                if (seen.Contains(threadId)) continue;

                var title = Html.HtmlToText(m.Groups[3].Value).Trim();
                if (title.Length == 0 || SkipTitle().IsMatch(title)) continue;

                seen.Add(threadId);
                outList.Add(new BoardThread(threadId, Html.DecodeEntities(m.Groups[1].Value), title));
            }
        }
        return outList;
    }

    /* ---------------- Einzelner Beitrag ---------------- */

    /// Erster Beitrag eines Threads. bodyHtml wird bewusst nicht uebernommen -
    /// die App zeigt nur Text.
    public static ParsedPost? ParsePost(string html)
    {
        var bodyHtml = MessageBody(html ?? "")?.Trim();
        if (bodyHtml is null) return null;

        var img = FirstImage().Match(bodyHtml);
        string? imageUrl = img.Success ? Html.DecodeEntities(img.Groups[1].Value) : null;
        // Smileys und Platzhalter sind keine Ankuendigungsbilder
        if (imageUrl is not null && !HttpUrl().IsMatch(imageUrl)) imageUrl = null;

        var h1 = Headline().Match(html ?? "");
        var title = Html.HtmlToText(h1.Success ? Badge().Replace(h1.Groups[1].Value, "") : "").Trim();

        DateTime? postedAt = null;
        var time = PostTime().Match(html ?? "");
        if (time.Success && DateTime.TryParse(
                time.Groups[1].Value, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            postedAt = parsed;

        return new ParsedPost(title, imageUrl, CleanText(Html.HtmlToText(bodyHtml)), postedAt);
    }

    [GeneratedRegex(@"^\s*Inhaltsverzeichnis\s*\[\s*Verbergen\s*\]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex TableOfContents();

    /// Das Inhaltsverzeichnis der Forensoftware steht am Anfang mancher
    /// Beitraege und sagt nichts ueber das Event aus.
    private static string CleanText(string text) => TableOfContents().Replace(text, "").Trim();

    /* ---------------- Zeitraum ---------------- */

    [GeneratedRegex(@"(\d{1,2})\.(\d{1,2})\.(\d{4})?")]
    private static partial Regex DateRe();

    [GeneratedRegex(@"(\d{1,2}):(\d{2})")]
    private static partial Regex TimeRe();

    // „Event-Start:" und „Start:" - beide Schreibweisen kommen im Forum vor,
    // die Jahreszahl fehlt oft ganz.
    [GeneratedRegex(@"\b(?:Event[-\s]?)?Start\s*:?\s*(\d{1,2})\.(\d{1,2})\.(\d{4})?", RegexOptions.IgnoreCase)]
    private static partial Regex EventStart();

    [GeneratedRegex(@"Event[-\s]?Ende\s*:?\s*(\d{1,2})\.(\d{1,2})\.(\d{4})?", RegexOptions.IgnoreCase)]
    private static partial Regex EventEnd();

    [GeneratedRegex(@"\b(?:am|vom|ab)\s+(\d{1,2}\.\d{1,2}\.(?:\d{4})?)", RegexOptions.IgnoreCase)]
    private static partial Regex SpanStart();

    [GeneratedRegex(@"(\d):(\d)")]
    private static partial Regex ClockDigits();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"(\d{1,2})\.(\d{1,2})\.(\d{4})?\s*[-–]\s*(\d{1,2})\.(\d{1,2})\.(\d{4})?")]
    private static partial Regex TitleRange();

    /// Der Satzteil hinter dem Startdatum, begrenzt vom Doppelpunkt, der die
    /// Angebotsliste einleitet - Uhrzeit-Doppelpunkte zaehlen nicht.
    private static string ClauseAfter(string rest)
    {
        var win = rest.Length > 160 ? rest[..160] : rest;
        // Ersatzzeichen gleicher Laenge, damit der Index zum Original passt
        var cut = ClockDigits().Replace(win, "$1 $2").IndexOf(':');
        return cut >= 0 ? win[..cut] : win;
    }

    /// Gueltigkeitszeitraum aus dem Beitragstext; der Titel ist nur Rueckfallebene.
    ///
    /// Die Jahreszahl fehlt in den Ankuendigungen haeufig („Event-Start: 08.10.
    /// um 00:00 Uhr"). Dann gilt das Jahr des Beitrags; liegt das Ende davor,
    /// ist der Jahreswechsel gemeint und es zaehlt das Folgejahr.
    public static Period ParsePeriod(string text, string title = "", int? fallbackYear = null)
    {
        var t = Spaces().Replace(text ?? "", " ");
        var year = fallbackYear ?? DateTime.UtcNow.Year;

        // Form „Event-Start: … Event-Ende: …" - die haeufigste im Board
        var evStart = EventStart().Match(t);
        if (evStart.Success)
        {
            var startYear = Year(evStart.Groups[3], year);
            var startsAt = Html.BerlinDate(startYear, G(evStart, 2), G(evStart, 1), 0, 0);

            var evEnd = EventEnd().Match(t);
            DateTime endsAt;
            var endYearGiven = false;

            if (evEnd.Success)
            {
                endYearGiven = evEnd.Groups[3].Success;
                endsAt = Html.BerlinDate(Year(evEnd.Groups[3], startYear), G(evEnd, 2), G(evEnd, 1), 23, 59);
            }
            else
            {
                // Ohne Ende gilt der Starttag
                endYearGiven = true;
                endsAt = Html.BerlinDate(startYear, G(evStart, 2), G(evStart, 1), 23, 59);
            }

            // Ende vor dem Start: ohne Jahresangabe ist der Jahreswechsel
            // gemeint, mit Jahresangabe ein Tippfehler im Beitrag.
            if (endsAt < startsAt)
            {
                endsAt = endYearGiven
                    ? Html.BerlinDate(startYear, G(evStart, 2), G(evStart, 1), 23, 59)
                    : Html.BerlinDate(startYear + 1, G(evEnd, 2), G(evEnd, 1), 23, 59);
            }

            return new Period(startsAt, endsAt, true);
        }

        Match? d1 = null;
        var after = -1;

        var span = SpanStart().Match(t);
        if (span.Success)
        {
            d1 = DateRe().Match(span.Groups[1].Value);
            after = span.Index + span.Length;
        }
        else
        {
            foreach (Match m in DateRe().Matches(t))
            {
                var end = m.Index + m.Length;
                var window = t[end..Math.Min(t.Length, end + 20)];
                // Eine Uhrzeit muss unmittelbar folgen
                if (TimeRe().IsMatch(window))
                {
                    d1 = m;
                    after = end;
                    break;
                }
            }
        }

        if (d1 is { Success: true })
        {
            var rest = ClauseAfter(t[after..]);
            var times = TimeRe().Matches(rest)
                .Select(m => (H: int.Parse(m.Groups[1].Value), M: int.Parse(m.Groups[2].Value)))
                .ToList();
            var d2 = DateRe().Match(rest);

            var (sh, sm) = times.Count > 0 ? times[0] : (0, 0);
            var startYear = Year(d1.Groups[3], year);
            var startsAt = Html.BerlinDate(startYear, G(d1, 2), G(d1, 1), sh, sm);

            var endYearGiven = !d2.Success || d2.Groups[3].Success;
            var (ey, em2, ed) = d2.Success
                ? (Year(d2.Groups[3], startYear), G(d2, 2), G(d2, 1))
                : (startYear, G(d1, 2), G(d1, 1));
            var (eh, emin) = times.Count > 1 ? times[1] : (23, 59);
            var endsAt = Html.BerlinDate(ey, em2, ed, eh, emin);

            var allDay = sh == 0 && sm == 0 && eh == 23 && emin == 59;

            if (endsAt < startsAt)
            {
                endsAt = endYearGiven
                    // Tippfehler im Enddatum: bis zum Ende des Starttags laufen
                    // lassen, nicht bis zu dessen Beginn.
                    ? Html.BerlinDate(startYear, G(d1, 2), G(d1, 1), 23, 59)
                    : Html.BerlinDate(ey + 1, em2, ed, eh, emin);
            }

            return new Period(startsAt, endsAt, allDay);
        }

        // Rueckfall: Datumsangaben im Titel
        var range = TitleRange().Match(title ?? "");
        if (range.Success)
        {
            var y2 = Year(range.Groups[6], Year(range.Groups[3], year));
            var y1 = Year(range.Groups[3], y2);
            return new Period(
                Html.BerlinDate(y1, G(range, 2), G(range, 1), 0, 0),
                Html.BerlinDate(y2, G(range, 5), G(range, 4), 23, 59),
                true);
        }

        var single = DateRe().Match(title ?? "");
        if (single.Success)
        {
            var y = Year(single.Groups[3], year);
            return new Period(
                Html.BerlinDate(y, G(single, 2), G(single, 1), 0, 0),
                Html.BerlinDate(y, G(single, 2), G(single, 1), 23, 59),
                true);
        }

        return new Period(null, null, false);
    }

    private static int G(Match m, int group) =>
        int.TryParse(m.Groups[group].Value, out var v) ? v : 0;

    /// Jahreszahl aus der Fundstelle, sonst die Vorgabe.
    private static int Year(Group group, int fallback) =>
        group.Success && int.TryParse(group.Value, out var v) ? v : fallback;

    /* ---------------- Einordnung ---------------- */

    /// Art der Itemshop-Aktion aus dem Titel.
    public static string Classify(string title)
    {
        var t = (title ?? "").ToLowerInvariant();
        if (t.Contains("happy hour")) return "happyhour";
        if (t.Contains("flash") || t.Contains("blitzauktion")) return "flash";
        if (t.Contains("rad")) return "wheel";
        if (t.Contains("sale") || t.Contains("rabatt")) return "sale";
        if (t.Contains("angebot im shop")) return "daily";
        return "other";
    }

    public static readonly Dictionary<string, string> KindLabels = new()
    {
        ["daily"] = "Tagesangebot",
        ["sale"] = "Sale",
        ["happyhour"] = "Happy Hour",
        ["flash"] = "Blitzaktion",
        ["wheel"] = "Rad",
        ["other"] = "Aktion",
    };

    [GeneratedRegex(@"\s*(?:,|&|\bund\b|\bsowie\b|\+)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex PartSplit();

    [GeneratedRegex(@"^(?:events?\s+im\s+doppelpack|events?\s+im\s+dreierpack|events?)\s*:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex PartPrefix();

    [GeneratedRegex(@"[!.…\s]+$")]
    private static partial Regex TrailingNoise();

    [GeneratedRegex(@"^[""„'\s]+")]
    private static partial Regex LeadingQuote();

    [GeneratedRegex(@"(drop|truhe|schatulle|beute|metin)")]
    private static partial Regex DropWords();

    [GeneratedRegex(@"(exp|erfahrung|yang|buff|bonus|rate)")]
    private static partial Regex BuffWords();

    /// Die einzelnen Events eines Sammelpostings; leer bei einem Einzel-Event.
    public static List<string> ParseParts(string title)
    {
        var t = (title ?? "").Trim();
        if (t.Length == 0) return [];

        var body = PartPrefix().Replace(t, "");
        var parts = PartSplit().Split(body)
            .Select(p => LeadingQuote().Replace(TrailingNoise().Replace(p, ""), "").Trim())
            .Where(p => p.Length > 2)
            .Distinct()
            .ToList();

        // Bleibt ein einzelner Name uebrig, steht der Titel selbst schon dafuer
        return parts.Count >= 2 ? parts : [];
    }

    public static string ClassifyGlobal(string title, string text = "")
    {
        var t = $"{title} {text}".ToLowerInvariant();
        if (DropWords().IsMatch(t)) return "drop";
        if (BuffWords().IsMatch(t)) return "buff";
        return "special";
    }

    public static readonly Dictionary<string, string> GlobalKindLabels = new()
    {
        ["drop"] = "Item Drop",
        ["buff"] = "Server Buff",
        ["special"] = "Special Event",
    };
}
