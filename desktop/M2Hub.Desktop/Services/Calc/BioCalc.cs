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

    /// Feste Zahl Akzeleratoren, um eine Abgabe zu ueberspringen, je Stufe.
    public sealed record AccelPerSkip(int P10, int P20, int P30);

    public sealed record Price(double Regular, double Sale, double? Flash = null);

    public sealed record BioSettings(
        AccelPerSkip PerSkipNormal,
        AccelPerSkip PerSkipSpecial,
        int[] SpecialLevels,
        Price P10,
        Price P20,
        Price P30,
        int ItemsPerPack,
        double PackRegular,
        double PackFlash,
        int DefaultPerDay,
        int DefaultCooldownMin);

    /// Werte aus der Referenzrechnung (wie in bio.ts). Flash-Sale gibt es nur
    /// fuer den 30-%-Akzelerator.
    public static readonly BioSettings Settings = new(
        PerSkipNormal: new(30, 14, 9),
        PerSkipSpecial: new(24, 12, 7),
        SpecialLevels: [92, 94],
        P10: new(12, 8),
        P20: new(25, 17),
        P30: new(45, 29, 22.5),
        ItemsPerPack: 10,
        PackRegular: 119,
        PackFlash: 79,
        DefaultPerDay: 10,
        DefaultCooldownMin: 60);

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

    /// Gehoert die Bio zu den guenstigeren Level-92/94-Bios? Champions zaehlen nie dazu.
    public static bool IsSpecial(Bio b, BioTab tab) =>
        tab == BioTab.Normal && Settings.SpecialLevels.Contains(b.Level);

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

    public sealed record BioOpts(int CooldownMin, int Already, int PerDay);

    public static BioResult Compute(IReadOnlyList<Bio> list, BioTab tab, BioOpts opts)
    {
        var s = Settings;
        var already = Math.Max(0, opts.Already);
        var perDay = Math.Max(1, opts.PerDay == 0 ? s.DefaultPerDay : opts.PerDay);
        var cooldownMin = Math.Max(0, opts.CooldownMin);

        var totalSubsRaw = list.Sum(b => b.Submissions);
        var totalSubs = Math.Max(0, totalSubsRaw - already);

        // Benoetigte Zeit = Abgaben x Cooldown je Abgabe.
        var totalHoursNone = totalSubs * cooldownMin / 60.0;
        var totalDays = (double)totalSubs / perDay;

        // Akzeleratoren je Skip sind feste Werte - Normal und die guenstigeren 92/94-Bios.
        var psN10 = s.PerSkipNormal.P10;
        var psN20 = s.PerSkipNormal.P20;
        var psN30 = s.PerSkipNormal.P30;
        var psH10 = tab == BioTab.Normal ? s.PerSkipSpecial.P10 : 0;
        var psH20 = tab == BioTab.Normal ? s.PerSkipSpecial.P20 : 0;
        var psH30 = tab == BioTab.Normal ? s.PerSkipSpecial.P30 : 0;

        // Gesamtbedarf je Bio: 92/94-Bios zaehlen mit den guenstigeren Werten.
        long acc10 = 0, acc20 = 0, acc30 = 0;
        foreach (var b in list)
        {
            var per = IsSpecial(b, tab) ? s.PerSkipSpecial : s.PerSkipNormal;
            acc10 += (long)b.Submissions * per.P10;
            acc20 += (long)b.Submissions * per.P20;
            acc30 += (long)b.Submissions * per.P30;
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
