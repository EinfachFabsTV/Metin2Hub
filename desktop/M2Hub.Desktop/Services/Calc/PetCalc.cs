namespace M2Hub.Desktop.Services.Calc;

/// Spieldaten und Rechenweg des Pet-Rechners: Fuetterbedarf und Werteprognose.
///
/// Portierung von 'Veraltet/shared/utils/calc/pet.ts', 'petexp.ts', 'petstats.ts'
/// und der Pet-Konstanten aus 'gamedata.ts'. Reine Funktionen ohne Bezug zur
/// Oberflaeche - die Zahlen stehen an einer Stelle und der Rechenweg bleibt
/// pruefbar. Die Werte stammen aus dem offiziellen Wiki bzw. eigener Messung.
public static class PetCalc
{
    /* ======================= Spieldaten ======================= */

    // Schluessel 50 bis 105 sind normale Level; ab 106 beginnen die Championlevel.
    public const int MinLevel = 50;
    public const int MaxLevel = 105;

    /// Versatz der Championlevel: Schluessel 106 steht fuer CL1.
    public const int ClOffset = 105;

    /// Leckerli+ heben das Pet unabhaengig von der Waffen-Tabelle: 20 Stueck je Level.
    public const int SnacksPerLevel = 20;

    /// Waffen +7 je Levelaufstieg (Item-EXP-Weg).
    public static readonly IReadOnlyDictionary<int, int> Weapons = new Dictionary<int, int>
    {
        [50] = 2, [51] = 1, [52] = 1, [53] = 1, [54] = 2, [55] = 2, [56] = 1, [57] = 2, [58] = 2, [59] = 2,
        [60] = 2, [61] = 3, [62] = 3, [63] = 3, [64] = 3, [65] = 4, [66] = 4, [67] = 4, [68] = 5, [69] = 5,
        [70] = 6, [71] = 7, [72] = 7, [73] = 8, [74] = 8, [75] = 10, [76] = 10, [77] = 12, [78] = 16, [79] = 18,
        [80] = 20, [81] = 22, [82] = 24, [83] = 27, [84] = 29, [85] = 32, [86] = 35, [87] = 39, [88] = 43, [89] = 47,
        [90] = 51, [91] = 72, [92] = 80, [93] = 87, [94] = 97, [95] = 106, [96] = 116, [97] = 128, [98] = 141, [99] = 142,
        [100] = 150, [101] = 153, [102] = 156, [103] = 159, [104] = 160, [105] = 162, [106] = 52, [107] = 72, [108] = 79, [109] = 88,
        [110] = 96, [111] = 106, [112] = 117, [113] = 128, [114] = 141, [115] = 142, [116] = 149, [117] = 154, [118] = 156, [119] = 158,
        [120] = 161, [121] = 161, [122] = 164, [123] = 164, [124] = 166, [125] = 167, [126] = 167, [127] = 168, [128] = 169, [129] = 170,
        [130] = 170, [131] = 170, [132] = 172, [133] = 172, [134] = 173,
    };

    /// Stacks "Koestliches Leckerli" je Levelaufstieg (normaler Pet-EXP-Weg).
    public static readonly IReadOnlyDictionary<int, double> DeliciousStacks = new Dictionary<int, double>
    {
        [50] = 0.2, [51] = 0.2, [52] = 0.2, [53] = 0.2, [54] = 0.2, [55] = 0.3, [56] = 0.3, [57] = 0.3, [58] = 0.3, [59] = 0.3,
        [60] = 0.4, [61] = 0.4, [62] = 0.5, [63] = 0.5, [64] = 0.5, [65] = 0.6, [66] = 0.7, [67] = 0.7, [68] = 0.8, [69] = 0.9,
        [70] = 1.0, [71] = 1.1, [72] = 1.2, [73] = 1.3, [74] = 1.4, [75] = 1.5, [76] = 1.7, [77] = 1.9, [78] = 2.7, [79] = 2.9,
        [80] = 3.2, [81] = 3.6, [82] = 3.9, [83] = 4.3, [84] = 4.7, [85] = 5.2, [86] = 5.7, [87] = 6.3, [88] = 6.9, [89] = 7.6,
        [90] = 8.4, [91] = 11.7, [92] = 12.9, [93] = 14.2, [94] = 15.6, [95] = 17.2, [96] = 18.9, [97] = 20.8, [98] = 22.8,
        [99] = 23.1, [100] = 24.2, [101] = 24.9, [102] = 25.3, [103] = 25.7, [104] = 26.0, [105] = 26.2,
        [106] = 0.7, [107] = 2.2, [108] = 4.3, [109] = 7.2, [110] = 10.8, [111] = 15.2, [112] = 20.2, [113] = 26.0,
        [114] = 32.5, [115] = 39.7, [116] = 47.7, [117] = 56.3, [118] = 65.7, [119] = 75.9, [120] = 86.7, [121] = 98.2,
        [122] = 110.5, [123] = 123.5, [124] = 137.3, [125] = 151.7, [126] = 166.9, [127] = 182.8, [128] = 199.4,
        [129] = 216.7, [130] = 234.8, [131] = 253.6, [132] = 273.1, [133] = 293.3,
    };

    /* ---------- Benoetigte Erfahrung ---------- */
    // Regel aus den Spieldaten: Benoetigte EXP = Item-EXP mal neun.

    /// Benoetigte EXP sind das Neunfache der Item-EXP.
    public const int ExpFactor = 9;

    /// Item-EXP je normalem Level (1 bis 105).
    public static readonly IReadOnlyDictionary<int, long> ItemExp = new Dictionary<int, long>
    {
        [1] = 15, [2] = 40, [3] = 75, [4] = 125, [5] = 215, [6] = 360, [7] = 550, [8] = 850, [9] = 1_200, [10] = 1_350,
        [11] = 2_150, [12] = 2_900, [13] = 3_800, [14] = 5_000, [15] = 6_500, [16] = 8_450, [17] = 10_950, [18] = 14_150,
        [19] = 18_250, [20] = 23_600, [21] = 30_500, [22] = 35_250, [23] = 40_650, [24] = 46_850, [25] = 53_850,
        [26] = 61_850, [27] = 70_900, [28] = 81_200, [29] = 92_850, [30] = 106_100, [31] = 121_050, [32] = 138_050,
        [33] = 157_250, [34] = 179_000, [35] = 203_650, [36] = 231_600, [37] = 255_300, [38] = 285_850, [39] = 313_200,
        [40] = 341_850, [41] = 380_000, [42] = 413_700, [43] = 449_500, [44] = 487_650, [45] = 528_000, [46] = 570_500,
        [47] = 616_000, [48] = 663_500, [49] = 714_000, [50] = 767_000, [51] = 843_500, [52] = 948_000, [53] = 999_000,
        [54] = 1_071_000, [55] = 1_146_500, [56] = 1_229_000, [57] = 1_310_000, [58] = 1_398_000, [59] = 1_490_000,
        [60] = 1_639_000, [61] = 1_803_000, [62] = 1_983_500, [63] = 2_182_000, [64] = 2_400_000, [65] = 2_640_000,
        [66] = 2_904_000, [67] = 3_194_500, [68] = 3_514_000, [69] = 3_865_500, [70] = 4_252_000, [71] = 4_677_000,
        [72] = 5_145_000, [73] = 5_660_000, [74] = 6_050_000, [75] = 6_850_000, [76] = 7_535_000, [77] = 8_285_500,
        [78] = 11_849_500, [79] = 13_032_500, [80] = 14_339_000, [81] = 15_769_000, [82] = 17_348_500,
        [83] = 19_084_000, [84] = 20_988_500, [85] = 23_088_000, [86] = 25_402_000, [87] = 27_937_000,
        [88] = 30_732_000, [89] = 33_806_500, [90] = 37_156_500, [91] = 52_061_100, [92] = 57_267_210,
        [93] = 62_993_931, [94] = 69_293_324, [95] = 76_222_656, [96] = 83_844_922, [97] = 92_229_414,
        [98] = 101_452_355, [99] = 102_500_000, [100] = 107_500_000, [101] = 110_500_000, [102] = 112_500_000,
        [103] = 114_000_000, [104] = 115_500_000, [105] = 116_500_000,
    };

    /// Item-EXP je Championlevel (CL1 bis CL30).
    public static readonly IReadOnlyDictionary<int, long> ClItemExp = new Dictionary<int, long>
    {
        [1] = 37_186_500, [2] = 52_059_100, [3] = 57_264_210, [4] = 62_993_931, [5] = 69_293_324,
        [6] = 76_222_656, [7] = 83_844_922, [8] = 92_229_414, [9] = 101_452_355, [10] = 102_500_000,
        [11] = 107_500_000, [12] = 110_500_000, [13] = 112_500_000, [14] = 114_000_000, [15] = 115_500_000,
        [16] = 116_500_000, [17] = 117_500_000, [18] = 118_500_000, [19] = 119_500_000, [20] = 120_000_000,
        [21] = 120_500_000, [22] = 121_000_000, [23] = 121_500_000, [24] = 122_000_000, [25] = 122_500_000,
        [26] = 123_000_000, [27] = 123_500_000, [28] = 124_000_000, [29] = 124_000_000, [30] = 124_500_000,
    };

    private static long SumRange(IReadOnlyDictionary<int, long> table, int from, int to)
    {
        var a = Math.Min(from, to);
        var b = Math.Max(from, to);
        var sum = 0L;
        for (var level = a; level < b; level++)
            if (table.TryGetValue(level, out var v)) sum += v;
        return sum;
    }

    /// Benoetigte Item-EXP fuer den Weg von 'from' nach 'to' (normale Level).
    public static long ItemExpBetween(int from, int to) => SumRange(ItemExp, from, to);

    /// Benoetigte Item-EXP zwischen zwei Championleveln.
    public static long ClItemExpBetween(int from, int to) => SumRange(ClItemExp, from, to);

    public static long ExpBetween(int from, int to) => ItemExpBetween(from, to) * ExpFactor;
    public static long ClExpBetween(int from, int to) => ClItemExpBetween(from, to) * ExpFactor;

    /* ======================= Fuetterbedarf ======================= */

    /// Auf welche Erfahrung zahlt das Futter ein?
    ///   Item - Item-EXP: Waffe +7 und Leckerli+
    ///   Exp  - normale Pet-EXP: Koestliches Leckerli
    public enum PetTrack { Item, Exp }

    /// Welche Zutat das Futter meint - fuer die Beschriftung unabhaengig von der Sprache.
    public enum FoodKind { Weapon, Snack, Delicious }

    public sealed record PetOption(FoodKind Kind, double Amount, PetTrack Track, double? Cost);

    public sealed record FeedResult(
        double Weapons,
        double Snacks,
        double DeliciousStacks,
        IReadOnlyList<PetOption> Options,
        /// Guenstigere der beiden Item-EXP-Varianten (Waffe/Leckerli+), falls Preise stehen.
        FoodKind? Best,
        /// Level ohne hinterlegten Wert fuers Koestliche Leckerli - die Stack-Angabe ist dann zu niedrig.
        IReadOnlyList<int> MissingDelicious);

    private static double Round1(double n) => Math.Round(n * 10) / 10;

    /// Fuetterbedarf und Kosten fuer den Weg von 'from' nach 'to'. Preise in kk
    /// optional: Waffe +7 je Stueck, Leckerli+ je Stueck, Koestliches Leckerli je Stack.
    public static FeedResult Feed(int from, int to, double? priceWeapon, double? priceSnack, double? priceDelicious)
    {
        var a = Math.Min(from, to);
        var b = Math.Max(from, to);

        double weapons = 0;
        double delicious = 0;
        var missing = new List<int>();
        for (var level = a; level < b; level++)
        {
            if (Weapons.TryGetValue(level, out var w)) weapons += w;
            if (DeliciousStacks.TryGetValue(level, out var d)) delicious += d;
            else missing.Add(level);
        }
        delicious = Round1(delicious);
        // Leckerli+ sind ein eigener Weg, nicht aus den Waffen umgerechnet.
        double snacks = (b - a) * SnacksPerLevel;

        static double? Cost(double amount, double? price) =>
            price is > 0 ? Math.Round(amount * price.Value * 100) / 100 : null;

        var options = new List<PetOption>
        {
            new(FoodKind.Weapon, weapons, PetTrack.Item, Cost(weapons, priceWeapon)),
            new(FoodKind.Snack, snacks, PetTrack.Item, Cost(snacks, priceSnack)),
            new(FoodKind.Delicious, delicious, PetTrack.Exp, Cost(delicious, priceDelicious)),
        };

        // Nur die beiden Item-EXP-Varianten sind wirklich austauschbar.
        var priced = options.Where(o => o.Cost is not null && o.Amount > 0 && o.Track == PetTrack.Item).ToList();
        FoodKind? best = priced.Count > 0 ? priced.Aggregate((m, o) => o.Cost! < m.Cost! ? o : m).Kind : null;

        return new FeedResult(weapons, snacks, delicious, options, best, missing);
    }

    /* ======================= Petwerte ======================= */

    /// Alle 'Every' Level eine Verbesserung um 'Min' bis 'Max' Prozent.
    public sealed record Growth(int Every, double Min, double Max);

    /// Startboni bei der Geburt (normale Level).
    public static readonly (double Min, double Max) StartBonusHp = (0.1, 2.3);
    public static readonly (double Min, double Max) StartBonusDef = (0.1, 2.0);
    public static readonly (double Min, double Max) StartBonusMp = (0.1, 2.0);

    /// Startbonus bei Championleveln.
    public static readonly (double Min, double Max) ClStartBonus = (0.1, 0.5);

    /// Werteverbesserungen je Pettyp (normale Level): hp, def, mp.
    public static readonly IReadOnlyDictionary<int, (Growth Hp, Growth Def, Growth Mp)> GrowthByType =
        new Dictionary<int, (Growth, Growth, Growth)>
        {
            [1] = (new(6, 0.1, 0.5), new(8, 0.1, 0.2), new(6, 0.1, 0.5)),
            [2] = (new(5, 0.2, 0.3), new(7, 0.2, 0.3), new(4, 0.2, 0.3)),
            [3] = (new(6, 0.1, 0.7), new(8, 0.1, 0.3), new(6, 0.1, 0.7)),
            [4] = (new(5, 0.3, 0.4), new(7, 0.3, 0.4), new(4, 0.3, 0.4)),
            [5] = (new(6, 0.1, 0.9), new(8, 0.1, 0.4), new(6, 0.1, 0.9)),
            [6] = (new(5, 0.5, 0.6), new(7, 0.4, 0.5), new(4, 0.4, 0.5)),
            [7] = (new(6, 0.1, 1.0), new(8, 0.1, 0.6), new(6, 0.1, 1.1)),
            [8] = (new(5, 0.5, 0.7), new(7, 0.5, 0.6), new(4, 0.5, 0.6)),
        };

    /// Werteverbesserungen je Pettyp (Championlevel): res, block, precision.
    public static readonly IReadOnlyDictionary<int, (Growth Res, Growth Block, Growth Precision)> ClGrowthByType =
        new Dictionary<int, (Growth, Growth, Growth)>
        {
            [1] = (new(5, 0.1, 0.5), new(5, 0.1, 0.5), new(4, 0.1, 0.5)),
            [2] = (new(4, 0.2, 0.3), new(4, 0.2, 0.3), new(3, 0.2, 0.3)),
            [3] = (new(5, 0.1, 0.7), new(5, 0.1, 0.7), new(4, 0.1, 0.7)),
            [4] = (new(4, 0.3, 0.4), new(4, 0.3, 0.4), new(3, 0.3, 0.4)),
            [5] = (new(5, 0.1, 0.9), new(5, 0.1, 0.9), new(4, 0.1, 0.9)),
            [6] = (new(4, 0.5, 0.6), new(4, 0.5, 0.6), new(3, 0.4, 0.5)),
            [7] = (new(5, 0.1, 1.0), new(5, 0.1, 1.0), new(4, 0.1, 1.1)),
            [8] = (new(4, 0.5, 0.7), new(4, 0.5, 0.7), new(3, 0.5, 0.6)),
        };

    // Je Tag wachsen die Verbesserungen um 0.001 Prozentpunkte, gedeckelt bei 0.8;
    // ab 800 Tagen steigt der Bonus nicht weiter. Gilt fuer TP, VTD und MP.
    public const double AgeBonusPerDay = 0.001;
    public const double AgeBonusMax = 0.8;
    public const int AgeMaxDays = 800;

    public static double AgeBonus(int days)
    {
        var d = Math.Max(0, Math.Min(AgeMaxDays, days));
        return Math.Min(AgeBonusMax, Math.Round(d * AgeBonusPerDay * 1000) / 1000);
    }

    public sealed record StatForecast(int Upgrades, double Min, double Max, double Avg);

    /// Anzahl Werteverbesserungen zwischen zwei Leveln.
    public static int UpgradeCount(int from, int to, int every)
    {
        if (every <= 0) return 0;
        var a = Math.Min(from, to);
        var b = Math.Max(from, to);
        return b / every - a / every;
    }

    /// Prognose eines Wertes: Startwert plus Verbesserungen, jeweils mit Alterungsbonus.
    public static StatForecast ForecastStat(double current, int from, int to, Growth g, int ageDays)
    {
        var upgrades = UpgradeCount(from, to, g.Every);
        var bonus = AgeBonus(ageDays);
        static double R3(double n) => Math.Round(n * 1000) / 1000;
        var min = R3(current + upgrades * (g.Min + bonus));
        var max = R3(current + upgrades * (g.Max + bonus));
        return new StatForecast(upgrades, min, max, R3((min + max) / 2));
    }

    public sealed record StatsResult(double AgeBonus, StatForecast A, StatForecast B, StatForecast C);

    /// Prognose fuer alle drei Werte eines Pets. Bei Championleveln sind das
    /// res/block/precision, sonst hp/def/mp - die Felder A/B/C in dieser Reihenfolge.
    public static StatsResult StatsForecast(int petType, int from, int to, int ageDays,
        double currentA, double currentB, double currentC, bool championLevel)
    {
        var type = Math.Min(8, Math.Max(1, petType));

        if (championLevel)
        {
            var g = ClGrowthByType[type];
            return new StatsResult(
                AgeBonus(ageDays),
                ForecastStat(currentA, from, to, g.Res, ageDays),
                ForecastStat(currentB, from, to, g.Block, ageDays),
                ForecastStat(currentC, from, to, g.Precision, ageDays));
        }
        else
        {
            var g = GrowthByType[type];
            return new StatsResult(
                AgeBonus(ageDays),
                ForecastStat(currentA, from, to, g.Hp, ageDays),
                ForecastStat(currentB, from, to, g.Def, ageDays),
                ForecastStat(currentC, from, to, g.Mp, ageDays));
        }
    }
}
