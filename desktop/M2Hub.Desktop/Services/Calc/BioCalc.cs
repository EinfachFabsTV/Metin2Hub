using System.Text.RegularExpressions;

namespace M2Hub.Desktop.Services.Calc;

/// Biologen-Quest: Akzeleratoren, Drachenmuenzen und Elixier-Packs bis zum
/// gewuenschten Cooldown.
///
/// Portierung von 'Veraltet/shared/utils/calc/bio.ts'. Reine Funktionen ohne
/// Bezug zur Oberflaeche. Grundgedanke: Jede Abgabe hat einen Grund-Cooldown;
/// ein Akzelerator senkt ihn um einen festen Anteil. Gesucht ist, wie viele
/// Akzeleratoren alle Abgaben kosten - und was das in Drachenmuenzen bedeutet.
///
/// Die Bio-Liste stand frueher in der Datenbank und wurde ueber den Admin
/// gepflegt. Ohne Server haelt die App sie als Katalog ('Catalog'); die Werte
/// stammen aus dem Biologen-Fenster im Spiel.
public static class BioCalc
{
    public sealed record Bio(
        int Id,
        int Level,
        string Name,
        int Submissions,
        string IconFile,
        string[] Effects,
        bool IsChampion = false,
        int Cl = 0);

    public enum BioTab { Normal, Champion }

    public sealed record Price(double Regular, double Sale, double? Flash = null);

    public sealed record BioSettings(
        /// Grund-Cooldown in Minuten: normale Abgaben 1440 (24 h), Lv 92/94 720 (12 h).
        int BaseMin,
        int Sp92,
        int Sp94,
        /// Multiplikator je Akzelerator-Stufe: 10 % -> 0,9 usw.
        double F10,
        double F20,
        double F30,
        Price P10,
        Price P20,
        Price P30,
        int ItemsPerPack,
        double PackRegular,
        double PackFlash,
        int DefaultPerDay);

    /// Werte aus der laufenden Referenz (M2Tools server/utils/bio-calc.ts) plus
    /// den dort gepflegten Preisen samt Flash-Sale (nur 30-%-Akzelerator).
    public static readonly BioSettings Settings = new(
        BaseMin: 1440,
        Sp92: 720,
        Sp94: 720,
        F10: 0.9,
        F20: 0.8,
        F30: 0.7,
        P10: new(12, 8),
        P20: new(25, 17),
        P30: new(45, 29, 22.5),
        ItemsPerPack: 10,
        PackRegular: 119,
        PackFlash: 79,
        DefaultPerDay: 10);

    /// Die normalen Biologen-Abgaben (Level 30 bis 94). Champion-Abgaben folgen
    /// spaeter, sobald die Daten vorliegen. Icons liegen in Assets/Bio, benannt
    /// wie das Feld 'IconFile'.
    public static readonly Bio[] Catalog =
    [
        new(1, 30, "Orkzahn", 10, "Orkzahn.png",
            ["+10% Bewegungsgeschwindigkeit"]),
        new(2, 40, "Fluchsammlung", 15, "Fluchsammlung.png",
            ["+5% Angriffsgeschwindigkeit"]),
        new(3, 50, "Dämonenandenken", 15, "Dämonenandenken.png",
            ["+60 Verteidigung"]),
        new(4, 60, "Eiskugel", 20, "Eiskugel.png",
            ["+50 Angriffswert"]),
        new(5, 70, "Zelkova Holzast", 25, "Zelkova Holzast.png",
            ["+11% Bewegungsgeschwindigkeit", "+10% Schadensreduzierung"]),
        new(6, 80, "Tugyis Tafel", 30, "Tugyis Tafel.png",
            ["+6% Angriffsgeschwindigkeit", "+10% Gesamtschaden"]),
        new(7, 85, "Roter Geisterbaum Ast", 40, "Roter Geisterbaum Ast.png",
            ["+10% Schadensreduzierung gegen Spieler"]),
        new(8, 90, "Notizen der Anführer", 50, "Notizen der Anführer.png",
            ["+8% mehr Schaden gegen Spieler"]),
        new(9, 92, "Juwel der Missgunst", 10, "Juwel der Missgunst.png",
            ["+1000 TP oder", "+120 Verteidigung oder", "+51 Angriffswert"]),
        new(10, 94, "Juwel der Weisheit", 20, "Juwel der Weisheit.png",
            ["+1100 TP oder", "+140 Verteidigung oder", "+60 Angriffswert"]),
    ];

    /// Wie viele Akzeleratoren braucht es, um den Cooldown von 'baseMin' auf
    /// 'targetMin' zu druecken? Jeder Einsatz multipliziert mit 'f' (< 1).
    /// Portiert aus bio-calc.ts.
    public static int AccelNeeded(int baseMin, int targetMin, double f)
    {
        if (targetMin <= 0 || targetMin >= baseMin) return 0;
        var k = (int)Math.Ceiling(Math.Log((double)targetMin / baseMin) / Math.Log(f));
        return Math.Max(0, k);
    }

    /// Grund-Cooldown je Abgabe: Lv 92/94 sind halbiert, alles andere voll.
    public static int BaseFor(Bio b, BioTab tab) =>
        tab == BioTab.Champion ? Settings.BaseMin
        : b.Level == 92 ? Settings.Sp92
        : b.Level == 94 ? Settings.Sp94
        : Settings.BaseMin;

    public sealed record BioResult(
        int TotalSubs,
        double TotalHoursNone,
        double TotalDays,
        // Akzeleratoren je Abgabe: normaler (N) und halbierter (H) Grund-Cooldown
        int PsN10, int PsN20, int PsN30,
        int PsH10, int PsH20, int PsH30,
        // Akzeleratoren insgesamt
        long Acc10, long Acc20, long Acc30,
        // Drachenmuenzen regulaer, im Angebot und im Flash-Sale (nur 30 %)
        double Dr10, double Dr20, double Dr30,
        double Drs10, double Drs20, double Drs30,
        double? Drf30,
        // Elixier-Packs: Anzahl, regulaer und Flash-Sale
        int Pk, double PkDr, double PkFlash);

    /// `TargetMin` ist der Ziel-Cooldown, den die Akzeleratoren erreichen sollen
    /// (das Eingabefeld „Cooldown"); er dient zugleich als Zeit je Abgabe.
    public sealed record BioOpts(int TargetMin, int Already, int PerDay);

    public static BioResult Compute(IReadOnlyList<Bio> list, BioTab tab, BioOpts opts)
    {
        var s = Settings;
        var already = Math.Max(0, opts.Already);
        var perDay = Math.Max(1, opts.PerDay == 0 ? s.DefaultPerDay : opts.PerDay);
        var target = Math.Max(0, opts.TargetMin);

        var totalSubsRaw = list.Sum(b => b.Submissions);
        var totalSubs = Math.Max(0, totalSubsRaw - already);

        // Benoetigte Zeit = Abgaben x Cooldown je Abgabe.
        var totalHoursNone = totalSubs * target / 60.0;
        var totalDays = (double)totalSubs / perDay;

        // Akzeleratoren je Skip haengen am Cooldown: wie oft beschleunigen, bis
        // der Grund-Cooldown auf das Ziel faellt. Normale Reihe aus BaseMin, die
        // guenstigere aus dem halbierten Cooldown der Lv-92/94-Abgaben.
        var psN10 = AccelNeeded(s.BaseMin, target, s.F10);
        var psN20 = AccelNeeded(s.BaseMin, target, s.F20);
        var psN30 = AccelNeeded(s.BaseMin, target, s.F30);
        var halfBase = Math.Min(s.Sp92, s.Sp94);
        var psH10 = tab == BioTab.Normal ? AccelNeeded(halfBase, target, s.F10) : 0;
        var psH20 = tab == BioTab.Normal ? AccelNeeded(halfBase, target, s.F20) : 0;
        var psH30 = tab == BioTab.Normal ? AccelNeeded(halfBase, target, s.F30) : 0;

        // Gesamtbedarf je Bio: 92/94-Bios rechnen mit ihrem halbierten Cooldown.
        long acc10 = 0, acc20 = 0, acc30 = 0;
        foreach (var b in list)
        {
            var baseMin = BaseFor(b, tab);
            acc10 += (long)b.Submissions * AccelNeeded(baseMin, target, s.F10);
            acc20 += (long)b.Submissions * AccelNeeded(baseMin, target, s.F20);
            acc30 += (long)b.Submissions * AccelNeeded(baseMin, target, s.F30);
        }

        var dr10 = acc10 * s.P10.Regular;
        var dr20 = acc20 * s.P20.Regular;
        var dr30 = acc30 * s.P30.Regular;
        var drs10 = acc10 * s.P10.Sale;
        var drs20 = acc20 * s.P20.Sale;
        var drs30 = acc30 * s.P30.Sale;
        // Flash-Sale nur fuer den 30-%-Akzelerator.
        double? drf30 = s.P30.Flash is { } flash ? acc30 * flash : null;

        // Elixier-Packs gibt es nur bei den normalen Bios; getrennt vom Akzelerator-Bedarf.
        var pk = 0;
        double pkDr = 0, pkFlash = 0;
        if (tab == BioTab.Normal)
        {
            pk = (int)Math.Ceiling((double)totalSubs / s.ItemsPerPack);
            pkDr = pk * s.PackRegular;
            pkFlash = pk * s.PackFlash;
        }

        return new BioResult(
            totalSubs, totalHoursNone, totalDays,
            psN10, psN20, psN30, psH10, psH20, psH30,
            acc10, acc20, acc30,
            dr10, dr20, dr30, drs10, drs20, drs30, drf30,
            pk, pkDr, pkFlash);
    }

    /// "1h30m", "90m", "1h", "45" → Minuten. Portiert aus der Website (biologen.vue).
    public static int ParseCooldown(string? input)
    {
        var s = (input ?? "").Trim().ToLowerInvariant();
        if (s.Length == 0) return 0;

        var hm = Regex.Match(s, @"(?:(\d+)\s*h)?\s*(?:(\d+)\s*m)?");
        var h = hm.Groups[1].Success ? int.Parse(hm.Groups[1].Value) : 0;
        var m = hm.Groups[2].Success ? int.Parse(hm.Groups[2].Value) : 0;
        if (h != 0 || m != 0) return h * 60 + m;

        var plain = Regex.Match(s, @"\d+");
        return plain.Success ? int.Parse(plain.Value) : 0;
    }
}
