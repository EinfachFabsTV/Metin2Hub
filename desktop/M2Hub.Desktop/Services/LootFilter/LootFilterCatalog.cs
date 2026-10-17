using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace M2Hub.Desktop.Services.LootFilter;

/// Loot-Filter-Katalog: der Kategoriebaum des Beuteoptionen-Fensters samt
/// Item-Bildern, eingelesen aus der gebuendelten Assets/LootFilter/
/// lootfilter.json (erzeugt von desktop/tools/import-lootfilter.sh).
///
/// Reines Nachschlagewerk - keine Ablage, kein Abruf. Faellt das Lesen aus,
/// bleibt der Katalog leer (fail-open). Waechst mit, sobald mehr Item-PNGs
/// nachgelegt und neu importiert werden.
public static class LootFilterCatalog
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
    private static IReadOnlyList<LootCategory>? _categories;
    private static IReadOnlyList<LootHit>? _allItems;

    public static IReadOnlyList<LootCategory> Categories => _categories ??= Load();

    /// Alle Items flach, mit ihrem Pfad (Ober- -> Unterkategorie ...) - die
    /// Grundlage der Suche „Item -> wohin gehoert es".
    public static IReadOnlyList<LootHit> AllItems => _allItems ??= Flatten();

    private static IReadOnlyList<LootCategory> Load()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://M2Hub/Assets/LootFilter/lootfilter.json"));
            var cats = JsonSerializer.Deserialize<LootFile>(stream, Opts)?.Categories ?? [];
            return Order(cats);
        }
        catch
        {
            return [];
        }
    }

    // Reihenfolge wie im Beuteoptionen-Fenster des Spiels (die Ordner kommen
    // alphabetisch, im Spiel stehen sie anders). Unbekannte - z. B. spaeter
    // ergaenzte - haengen hinten an, damit nichts verschwindet.
    private static readonly string[] TopOrder =
    [
        "waffen", "ruestungen", "helm", "sonstige-ausruestung", "kostuem",
        "drachensteinalchemie", "einzigartig", "veredelung", "traenke",
        "fischerei-bergbau", "reittiere-pets", "fertigkeitsbuch",
        "anderer-gegenstand", "eventitem",
    ];

    private static readonly Dictionary<string, string[]> SubOrder = new()
    {
        ["sonstige-ausruestung"] = ["schuhe", "guertel", "armbaender", "halsketten", "ohrringe", "schilde", "handschuhe", "talismane", "angelruten", "spitzhacken"],
        ["kostuem"] = ["waffenskins", "kostueme", "frisuren", "baender", "auren", "andere"],
        ["drachensteinalchemie"] = ["cor-draconis", "andere"],
        ["einzigartig"] = ["konsumgueter", "andere"],
        ["veredelung"] = ["materialien", "geiststeine", "erhoeht-die-wahrscheinlichkeit", "andere"],
        ["traenke"] = ["verbrauchsgueter", "haarfarben", "andere"],
        ["fischerei-bergbau"] = ["nahrung", "erze-steine", "andere"],
        ["reittiere-pets"] = ["pets", "reittiere", "pet-zubehoer", "pet-eier"],
        ["anderer-gegenstand"] = ["geschenkboxen", "hochzeit-und-ehe", "pergamente", "gruppe", "verwandlung", "herstellung", "pfeile", "andere", "eventitem"],
    };

    private static List<LootCategory> Order(IReadOnlyList<LootCategory> cats)
    {
        var top = Sort(cats, TopOrder);
        foreach (var c in top)
            c.Subs = Sort(c.Subs, SubOrder.GetValueOrDefault(c.Key));
        return top;
    }

    /// Erst die bekannten Schluessel in der vorgegebenen Reihenfolge, dann der
    /// Rest in seiner urspruenglichen (alphabetischen) Folge.
    private static List<LootCategory> Sort(IReadOnlyList<LootCategory> cats, string[]? order)
    {
        if (order is null || order.Length == 0) return cats.ToList();

        var byKey = new Dictionary<string, LootCategory>(StringComparer.Ordinal);
        foreach (var c in cats) byKey.TryAdd(c.Key, c);

        var result = new List<LootCategory>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var k in order)
            if (byKey.TryGetValue(k, out var c)) { result.Add(c); used.Add(k); }
        foreach (var c in cats)
            if (!used.Contains(c.Key)) result.Add(c);
        return result;
    }

    private static List<LootHit> Flatten()
    {
        var list = new List<LootHit>();
        foreach (var c in Categories) Walk(c, c.Key, new List<string>(), list);
        return list;
    }

    private static void Walk(LootCategory node, string topKey, List<string> path, List<LootHit> outp)
    {
        path.Add(node.Name);
        foreach (var it in node.Items) outp.Add(new LootHit(it.Name, path.ToArray(), it.File, topKey));
        foreach (var sub in node.Subs) Walk(sub, topKey, path, outp);
        path.RemoveAt(path.Count - 1);
    }

    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.Ordinal);

    /// Ein mitgeliefertes Item-Bild laden (einmal je Datei). `file` ist der
    /// relative Pfad aus der JSON, z. B. "LootFilter/kostuem/baender/x.png".
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

/// Wurzel der lootfilter.json.
public sealed class LootFile
{
    public List<LootCategory> Categories { get; set; } = [];
}

/// Ein Knoten im Kategoriebaum: Items direkt darin und Unterkategorien.
public sealed class LootCategory
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public List<LootItem> Items { get; set; } = [];
    public List<LootCategory> Subs { get; set; } = [];
}

/// Ein Item mit seinem Bild.
public sealed class LootItem
{
    public string Name { get; set; } = "";
    public string File { get; set; } = "";
}

/// Ein Suchtreffer: Item-Name, Pfad (Ober- -> Unterkategorie ...), Bild und
/// der Schluessel der Oberkategorie (fuer das Setzen im Filter-Editor).
public sealed record LootHit(string Name, string[] Path, string File, string TopKey);
