using System.Text.Json.Serialization;

namespace M2Hub.Desktop.Models;

// Datenmodell der App. Die Forum-Parser fuellen diese Typen, der lokale
// Speicher legt sie als JSON ab - es gibt keine Datenbank und keinen Server.

/* ---------- Accounts ---------- */

public sealed class AccountDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public int Sort { get; set; }

    /// Client-Sprache. Gameforge begrenzt die Zahl der Accounts je Sprache,
    /// deshalb steht sie an jedem Account (verweist auf LanguageDto.Id).
    public int? LanguageId { get; set; }

    /// Drachenmuenzen auf diesem Account.
    public int DragonCoins { get; set; }

    /// Server, auf dem der Account spielt (Schluessel aus dem Kalender).
    /// Leer heisst: keinem Server zugeordnet.
    public string ServerKey { get; set; } = "";

    /// Beschriftung zum Schluessel. Sie wird mitgespeichert, damit der Account
    /// lesbar bleibt, wenn der Kalender dieses Servers gerade nicht vorliegt -
    /// nach einem Sprachwechsel etwa.
    public string ServerLabel { get; set; } = "";

    public List<CharacterDto> Characters { get; set; } = new();
}

/// Client-Sprache mit eigener Farbe. Drei sind voreingestellt, weitere lassen
/// sich in der App anlegen.
public sealed class LanguageDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// Farbe als #RRGGBB - damit sich der Accountname sofort zuordnen laesst.
    public string Color { get; set; } = "#9CA3AF";
    public int Sort { get; set; }
}

public sealed class CharacterDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int? GuildId { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Medals { get; set; }
    public int Sort { get; set; }

    /// Rollen. Frei kombinierbar - ein Char kann zugleich Meley-Char und
    /// Bio-Char sein.
    public bool IsMeley { get; set; }

    /// Levelt fuer Meley und andere Laeufe ("Grotte") - im Bild rot.
    public bool IsGrotte { get; set; }

    /// Balathor-Char. Wie Meley eine eigene Rolle, unabhaengig davon.
    public bool IsBalathor { get; set; }

    /// Schlangenrun-Char, ebenfalls eigenstaendig.
    public bool IsSerpent { get; set; }

    /// Spenden-Char: auf ihn werden die Medaillen der Gilde gutgeschrieben.
    /// Die Sammelvergabe kann sich auf diese Rolle beschraenken.
    public bool IsDonate { get; set; }

    /// Traegt die Orkzahn-Bio dieses Accounts. Sie muss einmal je Account auf
    /// einem Char erledigt werden, weil die Tombola je Account gilt.
    public bool IsBio { get; set; }

    /// Bio auf diesem Char abgeschlossen.
    public bool BioDone { get; set; }
}

public sealed class GuildDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Level { get; set; } = 20;
    public int Sort { get; set; }
}

public sealed class PresetDto
{
    public string Label { get; set; } = "";
    public int Value { get; set; }
}

/* ---------- Serverspezifische Events (Kalender aus dem Forum) ---------- */

public sealed class TimeColDto
{
    public string Label { get; set; } = "";
    public int From { get; set; }
    public int To { get; set; }
}

public sealed class CalRowDto
{
    public string Label { get; set; } = "";
    public int? D { get; set; }
    public int? Weekday { get; set; }
    public List<string> Cells { get; set; } = new();
}

public sealed class CurrentDto
{
    /// Der Eventname, so wie er im deutschen Forum steht - uebersetzt wird
    /// erst beim Anzeigen.
    public string Text { get; set; } = "";

    /// Zeitspalte, in der es gerade laeuft. Als Zahlen, damit die Zeile
    /// („Jetzt (16–20)") in der eingestellten Sprache entstehen kann.
    public int From { get; set; }
    public int To { get; set; }
}

public sealed class ServerCalDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "date";
    public List<TimeColDto> Columns { get; set; } = new();
    public List<CalRowDto> Rows { get; set; } = new();
    public List<string> Specials { get; set; } = new();
    public CurrentDto? Current { get; set; }
}

/* ---------- Globale Events ---------- */

public sealed class GlobalEventDto
{
    public int Id { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "special";
    public List<string> Parts { get; set; } = new();
    public string? ImageUrl { get; set; }
    // bodyHtml gibt es hier nicht - die App rendert kein HTML.
    public string BodyText { get; set; } = "";
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? PostedAt { get; set; }

    /// Verfasser des Beitrags, sofern ablesbar. Aeltere cache.json kennen das
    /// Feld nicht - dort bleibt es leer.
    public string? Author { get; set; }

    public DateTime? FetchedAt { get; set; }
}

/* ---------- Itemshop ---------- */

public sealed class ItemshopEventDto
{
    public int Id { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "other";
    public string? ImageUrl { get; set; }
    public string BodyText { get; set; } = "";
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime? PostedAt { get; set; }

    /// Verfasser des Beitrags, sofern ablesbar.
    public string? Author { get; set; }

    public DateTime? FetchedAt { get; set; }
}


/* ---------- Run Tracker ---------- */

/// Ein eingetragener Lauf. Die Beute steht als Name → Anzahl; so bleibt die
/// Datei lesbar, auch wenn der Katalog spaeter eine Beuteart mehr kennt.
public sealed class RunEntryDto
{
    public int Id { get; set; }

    /// Schluessel des Laufs (RunCatalog.Run.Key).
    public string Run { get; set; } = "";

    /// Tag, an dem der Lauf zaehlt - als YYYY-MM-DD. Ein Datum ohne Uhrzeit,
    /// weil die Statistik nach Tagen und Monaten rechnet.
    public string Day { get; set; } = "";

    /// Erhaltene Truhen. Bei Laeufen ohne eigenes Feld die Summe der Beute.
    public int Chests { get; set; }

    /// Preis je Truhe **in kk**, wie er an diesem Tag galt. Der Preis
    /// schwankt; rechnete der Monat mit dem heutigen, waere die Summe falsch,
    /// sobald er sich einmal geaendert hat.
    ///
    /// 0 heisst „nicht vermerkt": Eintraege aus Fassungen vor 1.41.0 kennen
    /// das Feld nicht, fuer sie gilt der hinterlegte Preis des Laufs.
    public decimal Price { get; set; }

    public Dictionary<string, int> Loot { get; set; } = new();

    /// Wann der Eintrag gemacht wurde - fuer die Reihenfolge in der Liste.
    public DateTime AddedAt { get; set; }
}

/// Alles, was der Run Tracker speichert. Liegt als runs.json im Profil,
/// getrennt von accounts.json - es sind andere Daten mit anderer Lebensdauer.
public sealed class RunsData
{
    public List<RunEntryDto> Entries { get; set; } = new();

    /// Preis je Truhe in Won, je Lauf. Fehlt einer, gilt die Vorgabe.
    public Dictionary<string, decimal> ChestPrice { get; set; } = new();

    /// Abweichende Abklingzeit in Minuten, je Lauf.
    public Dictionary<string, int> Cooldown { get; set; } = new();

    public int NextId { get; set; } = 1;

    public int TakeId() => NextId++;
}

/// Ein Ziel: „1500w fuer X". Was die Laeufe seit `StartedAt` einbringen,
/// zaehlt automatisch; Ausgaben und Zusatzeinnahmen traegt der Nutzer ein.
public sealed class GoalDto
{
    public int Id { get; set; }

    /// Wofuer gespart wird - der Text, der im Stream steht.
    public string Title { get; set; } = "";

    /// Das Ziel in **Won**, so wie man es sagt.
    public decimal Target { get; set; }

    /// Ab wann die Laeufe zaehlen. Beim Anlegen „jetzt"; wird das Ziel durch
    /// ein abgeschlossenes vorheriges aktiv, ist es dessen Abschluss.
    public DateTime StartedAt { get; set; } = DateTime.Now;

    /// Was vom vorigen Ziel uebrig blieb - nur, wenn der Ueberschuss
    /// uebertragen werden soll.
    public decimal CarryIn { get; set; }

    /// Der Lauf, mit dem gerade gearbeitet wird. **Nur Anzeige und
    /// Schaetzung**: in den Gewinn zaehlen alle Laeufe ab `StartedAt`.
    public string ActiveRun { get; set; } = "hydra";

    /// Abgeschlossen - dann ist das naechste Ziel an der Reihe.
    public bool Done { get; set; }
    public DateTime? DoneAt { get; set; }

    /// Reihenfolge der Warteschlange: das kleinste offene Ziel ist das aktive.
    public int Sort { get; set; }

    /// Ausgaben und Zusatzeinnahmen, einzeln.
    public List<GoalBookingDto> Bookings { get; set; } = new();
}

/// Ein Posten unter einem Ziel. Die Art entscheidet das Vorzeichen:
/// `income` kommt dazu, alles andere geht ab.
public sealed class GoalBookingDto
{
    public int Id { get; set; }

    /// "push" (Standard: Traenke, Taus, Energiekristall), "extra"
    /// (Zusatz-Push, etwa Kostueme), "expense" (einmalige Ausgabe) oder
    /// "income" (Zusatzeinnahme).
    public string Kind { get; set; } = "push";

    /// Betrag in **Won**, immer positiv - das Vorzeichen steckt in der Art.
    public decimal Amount { get; set; }

    public string Note { get; set; } = "";
    public DateTime AddedAt { get; set; } = DateTime.Now;
}

/// Die Ziele, wie sie in goals.json stehen.
public sealed class GoalsData
{
    public List<GoalDto> Goals { get; set; } = new();

    /// Was beim Erreichen eines Ziels geschieht: "carry" (naechstes wird
    /// aktiv, der Ueberschuss zaehlt mit), "reset" (naechstes wird aktiv,
    /// faengt aber bei null an) oder "manual" (es bleibt stehen, bis der
    /// Nutzer umschaltet).
    public string OnReached { get; set; } = "carry";

    public int NextId { get; set; } = 1;

    public int TakeId() => NextId++;
}
