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

    /// Serpent-Segment-Char, ebenfalls eigenstaendig.
    public bool IsSerpent { get; set; }

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
    public DateTime? FetchedAt { get; set; }
}

