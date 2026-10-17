using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace M2Hub.Desktop.Services.Okey;

/// Die Truhen-Loot-Tabellen des Okey-Events, eingelesen aus der gebuendelten
/// Assets/Okey/okey-loot.json (erzeugt von desktop/tools/import-okey-loot.sh).
/// Reines Nachschlagewerk; faellt das Lesen aus, bleibt es leer (fail-open).
public static class OkeyLoot
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    private static LootFile? _data;

    public static LootFile Data => _data ??= Load();

    private static LootFile Load()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://M2Hub/Assets/Okey/okey-loot.json"));
            return JsonSerializer.Deserialize<LootFile>(stream, Opts) ?? new LootFile();
        }
        catch
        {
            return new LootFile();
        }
    }

    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.Ordinal);

    public static Bitmap? Image(string? file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        if (Cache.TryGetValue(file, out var cached)) return cached;

        Bitmap? bitmap = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://M2Hub/Assets/{file}"));
            bitmap = new Bitmap(stream);
        }
        catch
        {
            // Fehlt das Bild, bleibt die Kachel leer.
        }

        Cache[file] = bitmap;
        return bitmap;
    }

    public static Bitmap? Icon => Image(Data.Icon);

    /// Truhen-Icon nach Schluessel (bronze/silber/gold).
    public static Bitmap? ChestIcon(string key) =>
        Data.ChestIcons.TryGetValue(key, out var f) ? Image(f) : null;

    public static Bitmap? ChestIconForTier(int tier) =>
        ChestIcon(tier >= 2 ? "gold" : tier >= 1 ? "silber" : "bronze");
}

public sealed class LootFile
{
    public string Icon { get; set; } = "";
    public Dictionary<string, string> ChestIcons { get; set; } = new();
    public List<LootSeason> Seasons { get; set; } = [];
}

public sealed class LootSeason
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public List<LootChest> Chests { get; set; } = [];
}

public sealed class LootChest
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public List<LootItemData> Items { get; set; } = [];
}

public sealed class LootItemData
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public string Chance { get; set; } = "";
}
