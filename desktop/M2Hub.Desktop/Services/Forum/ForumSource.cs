namespace M2Hub.Desktop.Services.Forum;

/// Woher die Daten kommen: aus dem **deutschen** Forum, und nur von dort.
///
/// Vorher holte die App je Oberflaechensprache ein anderes Forum. Das hiess:
/// vier Board-Adressen, monatlich wechselnde Kalender-Threads, die erst gesucht
/// werden mussten, vier Schreibweisen der Monatsnamen und ein Zwischenspeicher,
/// der beim Sprachwechsel wegzuwerfen war. Vier Wege, auf denen etwas schief
/// gehen konnte - und drei davon konnte niemand hier nachstellen.
///
/// Jetzt gibt es einen Weg. Der deutsche Eventkalender steht dauerhaft im
/// selben Thread (je Server ein Beitrag, je Monat ein neuer), die beiden Boards
/// bleiben ebenfalls bestehen. Uebersetzt wird stattdessen beim Anzeigen
/// (siehe Glossary) - ohne Netz, ohne Server, und ohne dass ein Sprachwechsel
/// einen neuen Abruf braucht.
public static class ForumSource
{
    public const string BaseUrl = "https://board.de.metin2.gameforge.com/";

    public const string EventsBoardUrl = BaseUrl + "index.php?board/1167-news-events/";
    public const string ItemshopBoardUrl = BaseUrl + "index.php?board/303-news-itemshop/";

    /// Der Eventkalender. Ein Thread, dauerhaft, mit einem Beitrag je Server.
    public const string CalendarThreadUrl = BaseUrl + "index.php?thread/90381-eventkalender/";
}
