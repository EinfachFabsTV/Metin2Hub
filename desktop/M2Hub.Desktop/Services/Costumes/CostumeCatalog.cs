using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace M2Hub.Desktop.Services.Costumes;

/// Kostuem-Set-Katalog: eingelesen aus der gebuendelten Assets/Sets/sets.json,
/// die der Importer (desktop/tools/import-sets.sh) aus dem Quellordner erzeugt.
///
/// Reines Nachschlagewerk - keine Ablage, kein Abruf. Faellt das Lesen aus,
/// bleibt der Katalog leer (fail-open), statt die App zu stoeren.
public static class CostumeCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    private static IReadOnlyList<PushRing>? _rings;

    public static IReadOnlyList<PushRing> Rings => _rings ??= Load();

    private static IReadOnlyList<PushRing> Load()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://M2Hub/Assets/Sets/sets.json"));
            return JsonSerializer.Deserialize<SetsFile>(stream, Opts)?.Rings ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// Ein mitgeliefertes Bild aus Assets laden (einmal je Datei). `file` ist der
    /// relative Pfad aus der JSON, z. B. "Sets/nazar-amulett/.../x.png".
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
}

/// Wurzel der sets.json.
public sealed class SetsFile
{
    public List<PushRing> Rings { get; set; } = [];
}

/// Ein Push-Ring mit seinen Sets.
public sealed class PushRing
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public List<CostumeSet> Sets { get; set; } = [];
}

/// Ein Set: Effektstufen (2/3/4 Teile), Kostuem-Varianten und der 4. Slot
/// (Reittier/Pet/Waffe) mit eigenen Werten.
public sealed class CostumeSet
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";

    /// Schluessel "2"/"3"/"4" -> die Effektzeilen.
    public Dictionary<string, List<string>> Effects { get; set; } = new();

    public List<CostumeGroup> Costumes { get; set; } = [];
    public List<SlotVariant> Mounts { get; set; } = [];
    public List<SlotVariant> Pets { get; set; } = [];
    public List<SlotVariant> Weapons { get; set; } = [];
}

/// Eine Kostuem-Variante (Tier/Farbe) mit ihren Einzelbildern.
public sealed class CostumeGroup
{
    public string Name { get; set; } = "";
    public List<SetImage> Images { get; set; } = [];
}

/// Ein Einzelbild einer Kostuem-Variante, mit erratenem Teil/Geschlecht.
public sealed class SetImage
{
    public string Name { get; set; } = "";
    public string Part { get; set; } = "";
    public string Gender { get; set; } = "";
    public string File { get; set; } = "";
}

/// Eine Variante des 4. Slots (Reittier/Pet/Waffe) mit eigenen Werten.
public sealed class SlotVariant
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
    public List<string> Stats { get; set; } = [];
}
