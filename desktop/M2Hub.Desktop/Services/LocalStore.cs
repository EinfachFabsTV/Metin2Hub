using System.Text.Json;
using M2Hub.Desktop.Models;

namespace M2Hub.Desktop.Services;

/// Alles liegt lokal im Nutzerprofil - die App hat keinen Server und kein Konto.
///
///   accounts.json  Accounts, Charaktere, Gilden, Schnellwahl
///   runs.json      eingetragene Laeufe des Run Trackers
///   goals.json     Ziele samt Ausgaben und Zusatzeinnahmen
///   cache.json     Events und Itemshop - abgelaufene hoechstens sieben Tage
///   images/        heruntergeladene Ankuendigungsbilder
public sealed class LocalStore
{
    /// Aufbewahrung abgelaufener Forum-Daten. Was noch laeuft oder erst
    /// ansteht, bleibt unabhaengig davon stehen.
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(7);

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static string Directory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "M2Hub");

    private static string AccountsPath => Path.Combine(Directory, "accounts.json");
    private static string RunsPath => Path.Combine(Directory, "runs.json");
    private static string GoalsPath => Path.Combine(Directory, "goals.json");
    private static string CachePath => Path.Combine(Directory, "cache.json");
    private static string SettingsPath => Path.Combine(Directory, "settings.json");

    private readonly object _lock = new();

    public AccountsData Accounts { get; private set; } = new();
    public CacheData Cache { get; private set; } = new();
    public RunsData Runs { get; private set; } = new();
    public GoalsData Goals { get; private set; } = new();
    public SettingsData Settings { get; private set; } = new();

    public LocalStore()
    {
        Accounts = Read<AccountsData>(AccountsPath) ?? Seed();
        Cache = Read<CacheData>(CachePath) ?? new CacheData();
        Runs = Read<RunsData>(RunsPath) ?? new RunsData();
        Goals = Read<GoalsData>(GoalsPath) ?? new GoalsData();
        Settings = Read<SettingsData>(SettingsPath) ?? new SettingsData();
        Prune();
        StampRunPrices();
    }

    /// Schreibt den Preis in Eintraege, die noch keinen haben.
    ///
    /// Bis 1.40 stand der Preis je Lauf, nicht je Eintrag. Diese Eintraege
    /// folgten danach weiter dem Preis des Laufs - aenderte man ihn fuer
    /// heute, rechneten alle alten Tage mit. Sie bekommen deshalb einmalig
    /// den zuletzt hinterlegten Preis, und von da an gehoert der Preis dem
    /// Tag. Genauer geht es nicht: was an einem alten Tag galt, wurde nie
    /// gespeichert.
    private void StampRunPrices()
    {
        var changed = false;
        foreach (var entry in Runs.Entries.Where(e => e.Price <= 0m))
        {
            entry.Price = Runs.ChestPrice.TryGetValue(entry.Run, out var price) && price > 0m
                ? price
                : Calc.RunCatalog.DefaultChestPrice;
            changed = true;
        }

        if (changed) SaveRuns();
    }

    private static AccountsData Seed()
    {
        var data = new AccountsData();
        // Erststart: die Schnellwahl der Vorgaengerversion
        data.Presets.Add(new PresetDto { Label = "+12", Value = 12 });
        data.Presets.Add(new PresetDto { Label = "+21", Value = 21 });
        // und die drei Client-Sprachen in den Farben der Excel-Vorlage
        data.Languages.Add(new LanguageDto { Id = data.TakeId(), Name = "German", Color = "#EF4444", Sort = 0 });
        data.Languages.Add(new LanguageDto { Id = data.TakeId(), Name = "France", Color = "#10B981", Sort = 1 });
        data.Languages.Add(new LanguageDto { Id = data.TakeId(), Name = "PT", Color = "#F59E0B", Sort = 2 });
        return data;
    }

    private static T? Read<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
        }
        catch
        {
            // Kaputte Datei darf den Start nicht verhindern - dann eben leer.
            return null;
        }
    }

    private void Write(string path, object data)
    {
        lock (_lock)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                // Erst daneben schreiben, dann ersetzen - ein Absturz mittendrin
                // laesst so die alte Version stehen.
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(data, Json));
                File.Move(tmp, path, overwrite: true);
            }
            catch
            {
                // Nicht schreibbares Profil darf die Sitzung nicht abbrechen.
            }
        }
    }

    public void SaveAccounts() => Write(AccountsPath, Accounts);

    public void SaveSettings() => Write(SettingsPath, Settings);

    public void SaveRuns() => Write(RunsPath, Runs);
    public void SaveGoals() => Write(GoalsPath, Goals);

    /// Verwirft den Forum-Stand; der naechste Abruf holt alles neu.
    public void ClearCache()
    {
        Cache = new CacheData();
        Write(CachePath, Cache);
    }

    public void SaveCache()
    {
        Prune();
        Write(CachePath, Cache);
    }

    /// Abgelaufenes aelter als sieben Tage fliegt raus.
    ///
    /// Frueher zaehlte allein das Abrufdatum. Ein Thread wird aber nur einmal
    /// geladen - danach bleibt sein Abrufdatum stehen, waehrend das Event
    /// weiterlaeuft. Ein Event ueber vier Wochen verschwand so nach sieben
    /// Tagen aus der Liste, obwohl es gerade lief.
    ///
    /// Deshalb entscheidet jetzt der Zeitraum: was laeuft oder erst ansteht,
    /// bleibt. Erst wenn es vorbei ist, zaehlt das Alter. Eintraege ohne
    /// Zeitraum - Ankuendigungen ohne Datum - werden wie bisher nach sieben
    /// Tagen verworfen, weil sich ihr Ende sonst nie feststellen laesst.
    public void Prune()
    {
        var now = DateTime.UtcNow;
        var limit = now - CacheLifetime;

        Cache.GlobalEvents.RemoveAll(e => Outdated(e.StartsAt, e.EndsAt, e.FetchedAt, now, limit));
        Cache.Itemshop.RemoveAll(e => Outdated(e.StartsAt, e.EndsAt, e.FetchedAt, now, limit));

        if (Cache.CalendarFetchedAt is { } c && c.ToUniversalTime() < limit)
        {
            Cache.Servers.Clear();
            Cache.CalendarFetchedAt = null;
        }
    }

    /// Laeuft noch oder steht an: bleibt. Sonst raus, sobald der Abruf sieben
    /// Tage her ist.
    private static bool Outdated(
        DateTime? startsAt, DateTime? endsAt, DateTime? fetchedAt, DateTime now, DateTime limit)
    {
        if (endsAt is { } end && end.ToUniversalTime() >= now) return false;

        // Ohne Ende, aber mit Beginn in der Zukunft: angekuendigt, noch nicht
        // gelaufen - das gehoert erst recht nicht weggeworfen.
        if (endsAt is null && startsAt is { } start && start.ToUniversalTime() >= now) return false;

        return fetchedAt is { } f && f.ToUniversalTime() < limit;
    }
}

/// Nutzerdaten. Ids werden lokal vergeben, es gibt keine Datenbank.
public sealed class AccountsData
{
    public int NextId { get; set; } = 1;
    public List<AccountDto> Accounts { get; set; } = new();
    public List<GuildDto> Guilds { get; set; } = new();
    public List<PresetDto> Presets { get; set; } = new();
    public List<LanguageDto> Languages { get; set; } = new();

    /// Selbst eingetragene Server. Die bekannten stehen fest im Programm
    /// (ServerCatalog); wer auf einem anderen spielt, traegt ihn hier ein.
    public List<string> CustomServers { get; set; } = new();

    /// Anzeigereihenfolge der Kacheln: eigene Reihenfolge, Name, Medaillen, Level.
    public string SortMode { get; set; } = "eigene";

    public int TakeId() => NextId++;
}

/// Aus dem Forum geladene Daten samt allem, was der naechste Abruf braucht.
public sealed class CacheData
{
    public List<GlobalEventDto> GlobalEvents { get; set; } = new();
    public List<ItemshopEventDto> Itemshop { get; set; } = new();

    public List<ServerCalDto> Servers { get; set; } = new();
    public DateTime? CalendarFetchedAt { get; set; }
    /// Monat, fuer den der Kalender gilt - beim Monatswechsel neu laden.
    public int CalendarMonth { get; set; }

    public Dictionary<string, BoardState> Boards { get; set; } = new();

    public DateTime? LastRefreshAt { get; set; }
    public string? LastError { get; set; }

    public BoardState State(string prefix)
    {
        if (!Boards.TryGetValue(prefix, out var s))
        {
            s = new BoardState();
            Boards[prefix] = s;
        }
        return s;
    }
}

/// Stand eines Boards fuer bedingte Abrufe und den Fehler-Cooldown.
public sealed class BoardState
{
    public string? ETag { get; set; }
    public string? LastModified { get; set; }
    public DateTime? LastFetchAt { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public int FailCount { get; set; }
    /// Threads, die schon geladen wurden - fuer sie faellt kein Request mehr an.
    public List<string> KnownThreads { get; set; } = new();
}

/// Einstellungen der App. Bewusst klein gehalten - alles, was hier landet,
/// muss auch erklaerbar sein.
public sealed class SettingsData
{
    /// Welcher Serverkalender sein laufendes Event in der Kopfzeile zeigt.
    /// Leer heisst: keiner. Sonst der Schluessel eines Servers.
    public string HeaderServer { get; set; } = "";

    /// Server, die nicht angezeigt werden sollen (Schluessel). Wer nur auf
    /// einem Server spielt, braucht die Reiter der anderen nicht.
    public List<string> HiddenServers { get; set; } = new();

    /// Sprache der Oberflaeche: "auto" folgt Windows, sonst de/en/tr/it.
    public string Language { get; set; } = "auto";

    /// Beim Start nach einer neueren Version sehen.
    public bool CheckUpdates { get; set; } = true;

    /// Beitraege im Event-Board ohne erkannten Zeitraum mitzeigen.
    /// Standardmaessig aus - meist sind es gar keine Events.
    public bool ShowUndatedEvents { get; set; }

    /// Startseite: "overview" zeigt nur zusammengefasst, "work" laesst auch
    /// eintragen. Der Umschalter sitzt auf der Seite selbst.
    public string DashboardMode { get; set; } = "overview";

    /// Welche Abschnitte die Startseite zeigt, in dieser Reihenfolge - je
    /// Betriebsart eine eigene Liste. So sind die beiden Arten zwei gespeicherte
    /// Anordnungen, zwischen denen der Umschalter wechselt. Leer heisst: die
    /// Vorgabe aus DashboardSections.Default.
    public List<string> DashboardOverview { get; set; } = new();
    public List<string> DashboardWork { get; set; } = new();

    /// Welcher Bereich beim Start geoeffnet wird.
    public string StartPage { get; set; } = "start";

    /// Im Itemshop nur die Beitraege des Teams zeigen. Im Board stehen
    /// gelegentlich Beitraege von Spielern; die sind keine Aktionen.
    public bool TeamPostsOnly { get; set; } = true;

    /// Namen der Team-Accounts, wie sie im Forum unter dem Beitrag stehen.
    /// Leer heisst: nicht filtern - lieber alles zeigen als das Falsche
    /// verschlucken. Ergaenzen laesst sich die Liste in den Einstellungen.
    public List<string> TeamNames { get; set; } = new();

    /// Version, auf die schon hingewiesen wurde - damit derselbe Hinweis nicht
    /// bei jedem Start erneut kommt.
    public string? SkippedVersion { get; set; }

    /// Die Fassung, deren Patchnotes schon gezeigt wurden. Leer heisst: noch
    /// keine - dann erscheinen sie beim naechsten Start einmal.
    public string? PatchnotesSeen { get; set; }

    /// Was in der Stream-Einblendung steht.
    public StreamData Stream { get; set; } = new();

    /// Stand des Schulden-Rechners. Er gehoert in die Einstellungen und nicht
    /// zu den Laeufen: es ist **eine** Schuld, kein Wert je Lauf.
    public DebtData Debt { get; set; } = new();
}

/// Einstellungen der Stream-Einblendung (`StreamOverlay`).
public sealed class StreamData
{
    /// Aus heisst: es werden keine Dateien geschrieben. Wer nicht streamt,
    /// soll keine Dateien im Profil liegen haben, die er nie bestellt hat.
    public bool Enabled { get; set; }

    /// Welcher Zeitraum in Truhen und Runs steht: "today", "session" oder
    /// "total".
    public string Scope { get; set; } = "today";

    /// Ab wann „seit Stream-Start" zaehlt. Der Knopf in den Einstellungen
    /// setzt ihn auf jetzt.
    public DateTime SessionStart { get; set; } = DateTime.Now;

    /// Die zweite Einblendung: das aktive Ziel. Eigene Datei, eigene Quelle in
    /// OBS - wer nur das Ziel zeigen will, blendet die andere Karte aus.
    public bool GoalEnabled { get; set; }

    /// Was in der Goal-Karte steht. Jedes Stueck einzeln abschaltbar: in einem
    /// Overlay zaehlt jede Zeile, die man nicht braucht, als Platz, den man
    /// verliert.
    public bool GoalShowProgress { get; set; } = true;
    public bool GoalShowNet { get; set; } = true;
    public bool GoalShowRuns { get; set; } = true;
    public bool GoalShowChests { get; set; }
    public bool GoalShowActiveRun { get; set; } = true;
}

/// Was der Schulden-Rechner behaelt.
///
/// **Eine Schuld fuer alles**: Schuld und Abbezahltes gelten unabhaengig vom
/// Lauf. Der Laufwechsel aendert nur die Schaetzung - „wie viele Hydra-Runs"
/// statt „wie viele Razador-Runs" -, nicht den Fortschritt.
public sealed class DebtData
{
    /// Schuld und bereits Abbezahltes, in **Won**.
    public decimal Total { get; set; } = 2000m;
    public decimal Farmed { get; set; }

    /// Der Lauf, mit dem zuletzt gerechnet wurde.
    public string Run { get; set; } = "hydra";

    /// Die Preisspanne je Truhe, in kk.
    public decimal PriceLow { get; set; } = 50m;
    public decimal PriceHigh { get; set; } = 62m;

    /// Statt der Spanne der Durchschnitt aus der Gesamtstatistik.
    public bool UseAveragePrice { get; set; }

    /// Truhen je Lauf von Hand statt aus den eigenen Eintraegen - fuer Laeufe,
    /// zu denen noch nichts eingetragen ist, und fuer eigene Annahmen.
    public bool UseManualChests { get; set; }
    public decimal ManualChests { get; set; } = 9m;
}
