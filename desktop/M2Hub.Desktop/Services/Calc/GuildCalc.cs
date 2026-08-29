namespace M2Hub.Desktop.Services.Calc;

/// Spieldaten und Rechenweg des Gilden-Rechners.
///
/// Portierung von 'Veraltet/shared/utils/calc/gamedata.ts' und
/// 'Veraltet/shared/utils/calc/guild.ts'. Reine Funktionen ohne Bezug zur
/// Oberflaeche - so bleibt der Rechenweg pruefbar und die Zahlen stehen an
/// einer Stelle.
public static class GuildCalc
{
    /* ---------- Spieldaten ---------- */

    public const int MinLevel = 20;
    public const int MaxLevel = 40;

    /// Groesste moegliche Mitgliederzahl einer Gilde - mehr koennen nicht spenden.
    public const int MaxMembers = 90;

    /// Charakterplaetze je Account im Normalfall.
    public const int CharsPerAccount = 5;

    /// Spenden je Charakter und Tag.
    public const int DonationsPerDay = 3;

    /// Nur der mittlere Spendenschein ist im Itemshop kaufbar: 19 DR je Schein.
    /// Der hohe Schein faellt als Beute, die kleine Spende laeuft ueber Yang.
    public const int TicketDragonCoins = 19;
    public const string TicketKey = "medium";

    /// EXP fuer den Aufstieg von 'level' auf 'level + 1'.
    public static long ExpForLevel(int level) => 4_500_000L + (level - MinLevel) * 225_000L;

    /// Spendenarten: EXP fuer die Gilde, Tapferkeitsmedaillen fuer den Charakter.
    public sealed record DonationType(string Key, long Exp, int Medals);

    public static readonly DonationType[] DonationTypes =
    [
        new("small", 1_000, 3),
        new("medium", 10_000, 10),
        new("high", 30_000, 30),
    ];

    public static DonationType TypeOf(string? key) =>
        DonationTypes.FirstOrDefault(t => t.Key == key) ?? DonationTypes[1];

    /* ---------- Ergebnis ---------- */

    public sealed record GuildResult(
        long TotalExp,
        long Donations,
        long PerChar,
        long Days,
        long Medals,
        long DragonCoins,
        long DragonCoinsPerChar,
        int Accounts,
        long PerAccount,
        long DragonCoinsPerAccount,
        bool Buyable);

    /// Gesamte Gilden-EXP fuer den Weg von 'from' nach 'to'.
    public static long ExpBetween(int from, int to)
    {
        var a = Math.Max(MinLevel, Math.Min(from, to));
        var b = Math.Min(MaxLevel, Math.Max(from, to));

        var sum = 0L;
        for (var level = a; level < b; level++) sum += ExpForLevel(level);
        return sum;
    }

    /// Spendenbedarf fuer einen Gildenaufstieg.
    ///
    /// Spenden = EXP / EXP je Spende, aufgerundet; je Charakter davon der
    /// Anteil, aufgerundet; Tage = Spenden je Charakter / 3, aufgerundet.
    public static GuildResult Donations(int from, int to, int chars, string donationKey)
    {
        var type = TypeOf(donationKey);
        var totalExp = ExpBetween(from, to);

        // Mehr als die Mitgliedergrenze der Gilde koennen nicht spenden.
        var n = Math.Clamp(chars, 1, MaxMembers);

        var donations = Ceil(totalExp, type.Exp);
        var perChar = Ceil(donations, n);

        // Nur die kaufbare Spendenart kostet Drachenmuenzen.
        var buyable = type.Key == TicketKey;
        var drEach = buyable ? TicketDragonCoins : 0;

        var accounts = (int)Ceil(n, CharsPerAccount);

        // Ein Account stemmt hoechstens das Gesamtziel - sonst stuende bei
        // wenigen Charakteren mehr je Account als insgesamt zu spenden ist.
        var perAccount = Math.Min(perChar * CharsPerAccount, donations);

        return new GuildResult(
            TotalExp: totalExp,
            Donations: donations,
            PerChar: perChar,
            Days: Ceil(perChar, DonationsPerDay),
            Medals: donations * type.Medals,
            DragonCoins: donations * drEach,
            DragonCoinsPerChar: perChar * drEach,
            Accounts: accounts,
            PerAccount: perAccount,
            DragonCoinsPerAccount: perAccount * drEach,
            Buyable: buyable);
    }

    /// Aufrunden ohne Umweg ueber Gleitkomma - die Zahlen werden gross.
    private static long Ceil(long value, long divisor) =>
        divisor <= 0 ? 0 : (value + divisor - 1) / divisor;
}
