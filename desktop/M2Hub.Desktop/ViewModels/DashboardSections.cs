namespace M2Hub.Desktop.ViewModels;

/// Die Abschnitte der Startseite: welche es gibt, welche in welcher
/// Betriebsart voreingestellt sind, und wie sie heissen.
///
/// Die Schluessel stehen in den Einstellungen; ein unbekannter Schluessel aus
/// einer aelteren Datei wird beim Lesen uebergangen, statt die Seite zu
/// zerlegen.
public static class DashboardSections
{
    public const string Stats = "stats";
    public const string Active = "active";
    public const string Calendar = "calendar";
    public const string Itemshop = "itemshop";
    public const string Runs = "runs";
    public const string Medals = "medals";
    public const string Bio = "bio";

    /// Betriebsarten der Startseite.
    public const string Overview = "overview";
    public const string Work = "work";

    /// Alle Abschnitte in ihrer natuerlichen Reihenfolge - danach richtet sich
    /// die Liste in den Einstellungen.
    public static readonly string[] All =
        [Stats, Active, Runs, Calendar, Itemshop, Medals, Bio];

    /// „Uebersicht" fasst nur zusammen, „Arbeitsflaeche" nimmt die Abschnitte
    /// dazu, in die man etwas eintraegt.
    public static string[] Default(string mode) => mode == Work
        ? [Medals, Bio, Runs, Stats, Active, Calendar, Itemshop]
        : [Stats, Active, Runs, Calendar, Itemshop];

    /// Abschnitte, in denen etwas eingetragen wird. In der Uebersicht sind sie
    /// nicht voreingestellt, abwaehlbar bleiben sie ueberall.
    public static bool IsWorkSection(string key) => key is Medals or Bio;

    public static string Label(string key) => Services.Loc.T($"start.section.{key}");
}
