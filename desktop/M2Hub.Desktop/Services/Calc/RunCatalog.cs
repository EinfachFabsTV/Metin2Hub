namespace M2Hub.Desktop.Services.Calc;

/// Die Laeufe, die der Run Tracker kennt, samt ihrer Beute.
///
/// Die Werte stammen aus der Vorlage (m2tracker.de) und sind deshalb
/// **Vorgaben, keine festen Wahrheiten**: Abklingzeit und Preis je Truhe
/// stehen in den Einstellungen des Laufs und lassen sich aendern. Was hier
/// steht, ist nur der Stand beim ersten Start.
///
/// Der Hinweis unter der Auswahl ist derselbe wie dort - er nennt die
/// uebliche Ausbeute und die Abklingzeit, damit man nicht nachschlagen muss.
public static class RunCatalog
{
    /// Ein Lauf: Schluessel, Name, seine Beutearten und die Vorgaben.
    ///
    /// `EntersChests` unterscheidet die beiden Eingabearten der Vorlage:
    /// Bei den meisten Laeufen wird die Zahl der Truhen eingetippt, bei Jotun
    /// nur der Lauf abgeschlossen - dort ergibt sich die Truhenzahl aus der
    /// Beute darunter.
    public sealed record Run(
        string Key,
        string Name,
        string Hint,
        int CooldownMinutes,
        bool EntersChests,
        string[] Loot,
        int? MaxPerDay = null,
        string? Chest = null)
    {
        /// Die Truhe, die der Lauf abwirft - sie steht neben dem Eingabefeld.
        /// Meist ist das die Beute selbst; bei der Hydra nicht, dort faellt
        /// eine Truhe, in der die aufgezaehlten Dinge stecken.
        public string ChestName => Chest ?? Loot[0];
    }

    /// Vorgabe fuer den Preis je Truhe, **in kk**. Gerechnet wird ueberall in
    /// kk, angezeigt ab 100 kk in w - siehe `Money.FormatYang`.
    public const int DefaultChestPrice = 40;

    public static readonly Run[] Runs =
    [
        new("hydra", "Hydra",
            "Hydra-Truhe",
            20, true,
            ["Gegenstand verzaubern B", "Gegenstand verstärken", "Purpur Ebenholzkasten",
             "Blauer Ebenholzkasten", "Grüner Ebenholzkasten"],
            Chest: "Hydra-Truhe"),

        new("razador", "Razador",
            "8–10 Truhen des Razador pro Run",
            30, true,
            ["Truhe des Razador"]),

        new("nemere", "Nemere",
            "8–10 Truhen des Nemere pro Run",
            240, true,
            ["Truhe des Nemere"]),

        // Hier wird nur der Lauf abgeschlossen; die Truhen ergeben sich aus
        // den beiden Beutearten.
        new("jotun", "Jotun",
            "9–11 Truhen des Bagjanamu + 8–10 Truhen des Jotun Thrym pro Run",
            180, false,
            ["Truhe des Bagjanamu", "Truhe des Jotun Thrym"]),

        new("beran", "Beran",
            "8–10 Truhen des Beran-Setaou pro Run",
            1440, true,
            ["Truhe des Beran-Setaou"],
            MaxPerDay: 6),

        new("schlangenrun", "Schlangenrun",
            "Schlangenschatz · 64 Truhen pro Run",
            60, true,
            ["Schlangenschatz"],
            MaxPerDay: 10),
    ];

    public static Run? Find(string? key) => Runs.FirstOrDefault(r => r.Key == key);

    /// „30 Minuten Cooldown", „4 Stunden Cooldown" - volle Stunden werden als
    /// Stunden geschrieben, alles andere in Minuten.
    public static string Cooldown(int minutes) => minutes switch
    {
        <= 0 => "",
        < 60 => Loc.T("runs.cooldown.minutes", minutes),
        _ when minutes % 60 == 0 => Loc.T("runs.cooldown.hours", minutes / 60),
        _ => Loc.T("runs.cooldown.minutes", minutes),
    };

    /// Die Zeile unter der Auswahl: Ausbeute, Abklingzeit und - wo es eine
    /// gibt - die Tagesgrenze.
    public static string Subtitle(Run run)
    {
        var parts = new List<string> { run.Hint, Cooldown(run.CooldownMinutes) };
        if (run.MaxPerDay is { } max) parts.Add(Loc.T("runs.maxPerDay", max));

        return string.Join(" · ", parts.Where(p => p.Length > 0));
    }
}
