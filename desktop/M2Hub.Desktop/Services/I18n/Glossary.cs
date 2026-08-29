using M2Hub.Desktop.Services.Forum;

namespace M2Hub.Desktop.Services;

/// Uebersetzt die Woerter, die aus dem Forum kommen.
///
/// Geladen wird nur noch das deutsche Forum - eine Quelle, ein Aufbau, keine
/// monatlich wechselnden Threads, die gefunden werden muessen. Uebersetzt wird
/// dafuer hier, beim Anzeigen. Das geht ohne Server und ohne Netz, weil der
/// Wortschatz des Kalenders geschlossen ist: es sind dieselben zwei Dutzend
/// Gegenstaende, dazu Wochentage und die Kopfzeile.
///
/// Was nicht in der Liste steht, bleibt deutsch stehen. Das ist Absicht -
/// lieber der Originaltext als eine erfundene Uebersetzung. Fliesstext aus den
/// Ankuendigungen wird deshalb gar nicht erst angetastet.
///
/// Die italienische Spalte traegt bei den Gegenstaenden die englischen Namen:
/// das italienische Forum schreibt sie selbst englisch.
public static class Glossary
{
    /// Deutsch → en, tr, it. Reihenfolge wie in Loc.Languages (de ist die Quelle).
    private static readonly Dictionary<string, string[]> Terms = new(StringComparer.Ordinal)
    {
        // ---- Gegenstaende des Eventkalenders ----
        // tuerkisch und englisch aus den Kalendern der jeweiligen Foren
        ["Bruce"] = ["Bruce", "Robin", "Bruce"],
        ["Buch des Anführers"] = ["Leader's Book", "Liderin Kitabı", "Leader's Book"],
        ["Cor Draconis"] = ["Cor Draconis", "Cor Draconis", "Cor Draconis"],
        ["Elixier der Sonne"] = ["Sun Extract", "Güneş Özütü", "Sun Extract"],
        ["Elixier des Mondes"] = ["Moon Extract", "Ay Özütü", "Moon Extract"],
        ["Exorzismus-Schriftrolle"] = ["Exorcism Scroll", "Kötü Ruh Kovma Kağıdı", "Exorcism Scroll"],
        ["Feines Tuch"] = ["Fine Cloth", "İnce Kumaş", "Fine Cloth"],
        ["Fischpuzzle"] = ["Fishing Event", "Balıkçılık", "Fishing Event"],
        ["Flamme des Drachen"] = ["Flame of the Dragon", "Ejderha Alevi", "Flame of the Dragon"],
        ["Gegenstand verstärken B"] = ["Reinforce Item B", "Arttırma Kağıdı", "Reinforce Item B"],
        ["Gegenstand verzaubern B"] = ["Enchant Item B", "Nesneyi Efsunla", "Enchant Item B"],
        ["Grüne Drachenbohne"] = ["Green Dragon Bean", "Yeşil Ejderha Fasülyesi", "Green Dragon Bean"],
        ["Inventarerweiterung"] = ["Inventory Expansion", "Envanter Genişletme", "Inventory Expansion"],
        ["Kleine Segnung"] = ["Small Orison", "Küçük Kutsama", "Small Orison"],
        ["Konzentriertes Lesen"] = ["Concentrated Reading", "Münzevi Tavsiyesi", "Concentrated Reading"],
        ["Mondlicht-Schatztruhe"] = ["Moonlight", "Ay Işığı", "Moonlight"],
        ["Muffin grün"] = ["Nugget (green)", "Nugget", "Nugget (green)"],
        ["Passierschein"] = ["Passage Ticket", "Geçiş Bileti", "Passage Ticket"],
        ["Pet-Bücherkiste"] = ["Pet Book Chest", "Evcil Hayvan Kitap Sandığı", "Pet Book Chest"],
        ["Purpur-Ebenholzkasten"] = ["Crimson Ebony Box", "Kızıl Abanoz Kutusu", "Crimson Ebony Box"],
        ["Segenskugel"] = ["Blessing Marble", "Kutsama Küresi", "Blessing Marble"],
        ["Segensschriftrolle"] = ["Blessing Scroll", "Kutsama Kağıdı", "Blessing Scroll"],
        ["Supersteine"] = ["Superstone", "Süper Taş", "Superstone"],
        ["Teleportationsring"] = ["Teleportation Ring", "Işınlanma Yüzüğü", "Teleportation Ring"],

        // ---- Wochentage, wie sie in der ersten Spalte stehen ----
        ["Montag"] = ["Monday", "Pazartesi", "Lunedì"],
        ["Dienstag"] = ["Tuesday", "Salı", "Martedì"],
        ["Mittwoch"] = ["Wednesday", "Çarşamba", "Mercoledì"],
        ["Donnerstag"] = ["Thursday", "Perşembe", "Giovedì"],
        ["Freitag"] = ["Friday", "Cuma", "Venerdì"],
        ["Samstag"] = ["Saturday", "Cumartesi", "Sabato"],
        ["Sonntag"] = ["Sunday", "Pazar", "Domenica"],
    };

    /// Nachschlagform: klein, ohne Satzzeichen, Umlaute ausgeschrieben - dieselbe
    /// wie bei der Bildzuordnung, damit „Nugget (grün)" und „Nugget" zusammenfallen.
    private static readonly Dictionary<string, string[]> ByKey = Build();

    private static Dictionary<string, string[]> Build()
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var (german, other) in Terms)
        {
            var key = EventIcons.Normalize(german);
            if (key.Length > 0) map[key] = other;
        }
        return map;
    }

    /// Ein einzelner Begriff. Unbekanntes bleibt, wie es im Forum steht.
    public static string Term(string? german)
    {
        var text = (german ?? "").Trim();
        if (text.Length == 0) return "";

        var index = Array.IndexOf(Loc.Languages, Loc.I.Language);
        // Deutsch ist die Quelle - da gibt es nichts zu tun.
        if (index <= 0) return text;

        // Der Klammerzusatz („(M)", „(grün)") gehoert nicht zum Namen, bleibt
        // aber am uebersetzten Namen erhalten.
        var key = EventIcons.Normalize(text);
        if (!ByKey.TryGetValue(key, out var other)) return text;

        var translated = index - 1 < other.Length ? other[index - 1] : "";
        return translated.Length > 0 ? translated : text;
    }
}
