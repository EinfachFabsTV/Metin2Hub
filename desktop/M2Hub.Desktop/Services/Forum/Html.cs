using System.Text.RegularExpressions;

namespace M2Hub.Desktop.Services.Forum;

/// Portierung von shared/utils/scraper/html.ts. Rein regex-basiert, ohne DOM -
/// dieselben Ergebnisse wie im Web, damit sich Aenderungen abgleichen lassen.
///
/// sanitizeHtml() fehlt bewusst: die App rendert kein HTML, sondern nur Text.
public static partial class Html
{
    private static readonly Dictionary<string, string> Named = new(StringComparer.Ordinal)
    {
        ["amp"] = "&", ["lt"] = "<", ["gt"] = ">", ["quot"] = "\"", ["apos"] = "'", ["nbsp"] = " ",
        ["auml"] = "ä", ["ouml"] = "ö", ["uuml"] = "ü", ["Auml"] = "Ä", ["Ouml"] = "Ö", ["Uuml"] = "Ü",
        ["szlig"] = "ß", ["ndash"] = "–", ["mdash"] = "—", ["bull"] = "•", ["hellip"] = "…",
        ["rsquo"] = "’", ["lsquo"] = "‘", ["rdquo"] = "”", ["ldquo"] = "“",
        ["eacute"] = "é",
    };

    [GeneratedRegex(@"&#x([0-9a-fA-F]+);")] private static partial Regex HexEntity();
    [GeneratedRegex(@"&#(\d+);")] private static partial Regex DecEntity();
    [GeneratedRegex(@"&([a-zA-Z]+);")] private static partial Regex NamedEntity();
    [GeneratedRegex(@"<[^>]*>")] private static partial Regex AnyTag();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"\(\s+")] private static partial Regex OpenParen();
    [GeneratedRegex(@"\s+\)")] private static partial Regex CloseParen();
    [GeneratedRegex(@"\s+([,;:!?])")] private static partial Regex BeforePunct();

    public static string DecodeEntities(string? s)
    {
        var value = s ?? "";
        value = HexEntity().Replace(value, m => FromCode(m.Groups[1].Value, 16));
        value = DecEntity().Replace(value, m => FromCode(m.Groups[1].Value, 10));
        value = NamedEntity().Replace(value, m =>
            Named.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        return value;
    }

    private static string FromCode(string digits, int fromBase)
    {
        try
        {
            var code = Convert.ToInt32(digits, fromBase);
            return code is > 0 and <= 0x10FFFF ? char.ConvertFromUtf32(code) : "";
        }
        catch
        {
            return "";
        }
    }

    /// Tags werden durch Leerzeichen ersetzt; danach werden die dadurch
    /// entstandenen Luecken bei Klammern und Satzzeichen wieder geschlossen.
    public static string HtmlToText(string? s)
    {
        var value = DecodeEntities(AnyTag().Replace(s ?? "", " "));
        value = Spaces().Replace(value, " ");
        value = OpenParen().Replace(value, "(");
        value = CloseParen().Replace(value, ")");
        value = BeforePunct().Replace(value, "$1");
        return value.Trim();
    }

    /* ---------- Berliner Zeit, unabhaengig von der Zone des Rechners ---------- */

    private static readonly TimeZoneInfo Berlin = FindBerlin();

    private static TimeZoneInfo FindBerlin()
    {
        foreach (var id in new[] { "Europe/Berlin", "W. Europe Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // Naechste Schreibweise versuchen
            }
            catch (InvalidTimeZoneException)
            {
                // dito
            }
        }
        return TimeZoneInfo.Local;
    }

    /// Ein Berliner Datum als absoluter Zeitpunkt (UTC).
    public static DateTime BerlinDate(int y, int m, int d, int hh = 0, int mm = 0)
    {
        // Ungueltige Kombinationen (Tippfehler im Forum) nicht zum Absturz fuehren lassen
        var days = DateTime.DaysInMonth(Math.Clamp(y, 1, 9999), Math.Clamp(m, 1, 12));
        var local = new DateTime(
            Math.Clamp(y, 1, 9999), Math.Clamp(m, 1, 12), Math.Clamp(d, 1, days),
            Math.Clamp(hh, 0, 23), Math.Clamp(mm, 0, 59), 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(Adjust(local), Berlin);
    }

    /// Zeitumstellung: die uebersprungene Stunde gibt es nicht, die doppelte
    /// waere mehrdeutig - beides auf einen gueltigen Zeitpunkt schieben.
    private static DateTime Adjust(DateTime local) =>
        Berlin.IsInvalidTime(local) ? local.AddHours(1) : local;

    public static DateTime ToBerlin(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc.ToUniversalTime(), Berlin);

    /// Berliner Kalendertag als YYYY-MM-DD.
    public static string BerlinDay(DateTime instant) => ToBerlin(instant).ToString("yyyy-MM-dd");

    public sealed record BerlinMoment(int Y, int M, int D, int Hour, int Weekday);

    /// Jetzt in Berlin; Wochentag 1 = Montag … 7 = Sonntag.
    public static BerlinMoment BerlinNow(DateTime? at = null)
    {
        var b = ToBerlin(at ?? DateTime.UtcNow);
        var weekday = b.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)b.DayOfWeek;
        return new BerlinMoment(b.Year, b.Month, b.Day, b.Hour, weekday);
    }

    private static readonly string[] Months =
    [
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember",
    ];

    public static string MonthDeName(int m) => m >= 1 && m <= 12 ? Months[m - 1] : "";
}
