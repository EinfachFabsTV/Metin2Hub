using System.Text.Json;
using M2Hub.Desktop.Models;

namespace M2Hub.Desktop.Services;

/// Alles liegt lokal im Nutzerprofil - die App hat keinen Server und kein Konto.
///
///   accounts.json  Accounts, Charaktere, Gilden, Schnellwahl
///   cache.json     Events und Itemshop, hoechstens sieben Tage alt
///   images/        heruntergeladene Ankuendigungsbilder
public sealed class LocalStore
{
    /// Aufbewahrung der geladenen Forum-Daten.
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
    private static string CachePath => Path.Combine(Directory, "cache.json");
    private static string SettingsPath => Path.Combine(Directory, "settings.json");

    private readonly object _lock = new();

    public AccountsData Accounts { get; private set; } = new();
    public CacheData Cache { get; private set; } = new();
    public SettingsData Settings { get; private set; } = new();

    public LocalStore()
    {
        Accounts = Read<AccountsData>(AccountsPath) ?? Seed();
        Cache = Read<CacheData>(CachePath) ?? new CacheData();
        Settings = Read<SettingsData>(SettingsPath) ?? new SettingsData();
        Prune();
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

    /// Alles aelter als sieben Tage fliegt raus - so war es abgesprochen.
    public void Prune()
    {
        var limit = DateTime.UtcNow - CacheLifetime;
        Cache.GlobalEvents.RemoveAll(e => e.FetchedAt is { } f && f.ToUniversalTime() < limit);
        Cache.Itemshop.RemoveAll(e => e.FetchedAt is { } f && f.ToUniversalTime() < limit);
        if (Cache.CalendarFetchedAt is { } c && c.ToUniversalTime() < limit)
        {
            Cache.Servers.Clear();
            Cache.CalendarFetchedAt = null;
        }
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

    /// Version, auf die schon hingewiesen wurde - damit derselbe Hinweis nicht
    /// bei jedem Start erneut kommt.
    public string? SkippedVersion { get; set; }
}
