using System.Globalization;
using System.Text.RegularExpressions;

namespace M2Hub.Desktop.Services.Costumes;

/// Rechenkern des Set-Simulators: welche Kombination aus Ring, Body, Frisur,
/// Waffe, Pet und Reittier die meiste EXP bringt.
///
/// Reine Funktionen auf Basis des Katalogs (CostumeCatalog). EXP entsteht aus
/// zwei Quellen, die sich addieren:
///   - Eigen-EXP einzelner Teile (Ring, Pet, Reittier, Waffe) aus ihren Werten;
///   - Set-Boni: je Set die hoechste erreichte Stufe (Zahl getragener Teile des
///     Sets, der Ring zaehlt mit, wenn man den Ring dieses Sets traegt).
public static class SetSimulator
{
    public enum Slot { Ring, Body, Frisur, Weapon, Pet, Mount }

    public static readonly Slot[] Slots =
        [Slot.Ring, Slot.Body, Slot.Frisur, Slot.Weapon, Slot.Pet, Slot.Mount];

    /// Ein waehlbares Teil fuer einen Slot.
    public sealed record SimItem(Slot Slot, string SetName, string Label, string? File,
        double OwnExp, string? SetKey, string RingKey, string Gender = "");

    private sealed record SetInfo(string Key, string Name, string RingKey, (int Count, double Exp)[] Tiers)
    {
        /// EXP der hoechsten Stufe, die mit `count` Teilen erreicht ist.
        public double ExpForCount(int count)
        {
            double exp = 0;
            foreach (var (c, e) in Tiers) if (c <= count) exp = e;
            return exp;
        }
    }

    /* ---------- EXP aus Text ---------- */

    private static readonly Regex ExpKey = new(@"\b(EXP|Erfahrung)", RegexOptions.IgnoreCase);
    private static readonly Regex Percent = new(@"([+-]?\d+(?:[.,]\d+)?)\s*%");

    /// EXP-Prozent aus einer Zeile („EXP +150%", „+50% Erfahrungspunkte"), sonst 0.
    public static double Exp(string line)
    {
        if (!ExpKey.IsMatch(line)) return 0;
        var m = Percent.Match(line);
        return m.Success
            ? double.Parse(m.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture)
            : 0;
    }

    public static double Exp(IEnumerable<string> lines) => lines.Sum(Exp);

    /* ---------- Katalog in Simulator-Form (einmal aufgebaut) ---------- */

    private static Dictionary<Slot, List<SimItem>>? _pools;
    private static Dictionary<string, SetInfo>? _sets;

    public static IReadOnlyDictionary<Slot, List<SimItem>> Pools => _pools ??= Build().pools;
    private static IReadOnlyDictionary<string, SetInfo> SetsByKey => _sets ??= Build().sets;

    private static (Dictionary<Slot, List<SimItem>> pools, Dictionary<string, SetInfo> sets) Build()
    {
        var pools = Slots.ToDictionary(s => s, _ => new List<SimItem>());
        var sets = new Dictionary<string, SetInfo>(StringComparer.Ordinal);

        foreach (var ring in CostumeCatalog.Rings)
        {
            pools[Slot.Ring].Add(new SimItem(Slot.Ring, ring.Name, ring.Name, ring.Icon,
                Exp(ring.Stats), null, ring.Key));

            foreach (var set in ring.Sets)
            {
                if (!sets.ContainsKey(set.Key))
                {
                    var tiers = set.Effects
                        .Select(kv => (Count: int.TryParse(kv.Key, out var n) ? n : 0, Exp: Exp(kv.Value)))
                        .Where(t => t.Count > 0)
                        .OrderBy(t => t.Count)
                        .ToArray();
                    sets[set.Key] = new SetInfo(set.Key, set.Name, ring.Key, tiers);
                }

                foreach (var c in set.Costumes)
                    foreach (var img in c.Images)
                    {
                        var slot = img.Part == "Frisur" ? Slot.Frisur : Slot.Body;
                        pools[slot].Add(new SimItem(slot, set.Name, $"{set.Name} · {img.Name}",
                            img.File, 0, set.Key, ring.Key, img.Gender));
                    }

                AddVariants(pools[Slot.Weapon], set.Weapons, Slot.Weapon, set, ring.Key);
                AddVariants(pools[Slot.Pet], set.Pets, Slot.Pet, set, ring.Key);
                AddVariants(pools[Slot.Mount], set.Mounts, Slot.Mount, set, ring.Key);
            }
        }

        return (pools, sets);
    }

    private static void AddVariants(List<SimItem> pool, List<SlotVariant> src, Slot slot,
        CostumeSet set, string ringKey)
    {
        foreach (var v in src)
            pool.Add(new SimItem(slot, set.Name, $"{set.Name} · {v.Name}", v.File,
                Exp(v.Stats), set.Key, ringKey));
    }

    /* ---------- Bewertung einer Auswahl ---------- */

    public sealed record Bonus(string Label, double Exp);

    public sealed record SimResult(double TotalExp, IReadOnlyList<Bonus> Own, IReadOnlyList<Bonus> SetBonuses);

    public static SimResult Evaluate(IReadOnlyDictionary<Slot, SimItem?> sel)
    {
        var own = new List<Bonus>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string? ringKey = null;

        foreach (var slot in Slots)
        {
            if (!sel.TryGetValue(slot, out var item) || item is null) continue;
            if (item.OwnExp > 0) own.Add(new Bonus(item.Label, item.OwnExp));
            if (slot == Slot.Ring) ringKey = item.RingKey;
            else if (item.SetKey is { } sk) counts[sk] = counts.GetValueOrDefault(sk) + 1;
        }

        // Der getragene Ring zaehlt als Teil jedes Sets, das unter ihm liegt.
        if (ringKey is not null)
            foreach (var sk in counts.Keys.ToList())
                if (SetsByKey.TryGetValue(sk, out var si) && si.RingKey == ringKey)
                    counts[sk]++;

        var bonuses = new List<Bonus>();
        foreach (var (sk, count) in counts)
        {
            if (count < 2 || !SetsByKey.TryGetValue(sk, out var si)) continue;
            var exp = si.ExpForCount(count);
            if (exp > 0) bonuses.Add(new Bonus($"{si.Name} ({count})", exp));
        }

        var total = own.Sum(b => b.Exp) + bonuses.Sum(b => b.Exp);
        return new SimResult(total, own, bonuses.OrderByDescending(b => b.Exp).ToList());
    }

    /* ---------- Optimierer ---------- */

    /// Beste gefundene Kombination fuer maximale EXP. Durchsucht Ring × bis zu
    /// zwei Fokus-Sets und fuellt die restlichen Slots mit dem EXP-staerksten
    /// Einzelteil. Deckt die ueblichen Muster (ein grosses Set + starke
    /// Pet/Reittier, oder 2+2) ab; kein Beweis auf das globale Optimum.
    /// Beste gefundene Kombination. Ist `fixedRing` gesetzt, bleibt dieser Ring
    /// stehen und es werden nur die uebrigen Slots optimiert.
    public static Dictionary<Slot, SimItem?> Best(SimItem? fixedRing = null)
    {
        var cosmetic = new[] { Slot.Body, Slot.Frisur, Slot.Weapon, Slot.Pet, Slot.Mount };

        // Je Slot das EXP-staerkste Einzelteil (fuer freie Slots).
        var bestOwn = Slots.ToDictionary(s => s,
            s => Pools[s].OrderByDescending(i => i.OwnExp).FirstOrDefault());

        // Je (Set, Slot) das beste Teil dieses Sets.
        var bySetSlot = new Dictionary<(string, Slot), SimItem>();
        foreach (var slot in cosmetic)
            foreach (var g in Pools[slot].Where(i => i.SetKey is not null).GroupBy(i => i.SetKey!))
                bySetSlot[(g.Key, slot)] = g.OrderByDescending(i => i.OwnExp).First();

        var setKeys = SetsByKey.Keys.Append<string?>(null).ToList(); // null = kein Fokus-Set
        var rings = fixedRing is not null
            ? new List<SimItem?> { fixedRing }
            : Pools[Slot.Ring].Append(null).ToList();

        Dictionary<Slot, SimItem?> best = Slots.ToDictionary(s => s,
            s => s == Slot.Ring && fixedRing is not null ? fixedRing : (SimItem?)bestOwn[s]);
        var bestTotal = Evaluate(best).TotalExp;

        foreach (var ring in rings)
            foreach (var a in setKeys)
                foreach (var b in setKeys)
                {
                    var sel = Slots.ToDictionary(s => s, _ => (SimItem?)null);
                    sel[Slot.Ring] = ring;
                    foreach (var slot in cosmetic)
                    {
                        SimItem? pick = null;
                        if (a is not null && bySetSlot.TryGetValue((a, slot), out var ia)) pick = ia;
                        else if (b is not null && bySetSlot.TryGetValue((b, slot), out var ib)) pick = ib;
                        sel[slot] = pick ?? bestOwn[slot];
                    }
                    var total = Evaluate(sel).TotalExp;
                    if (total > bestTotal) { bestTotal = total; best = sel; }
                }

        return best;
    }
}
