namespace M2Hub.Desktop.Services.Calc;

/// Schulden-Rechner: wie viele Laeufe noch fehlen, bis eine Schuld bezahlt
/// ist.
///
/// Reine Funktionen, wie bei `GuildCalc` - gerechnet wird in **kk**, wie
/// ueberall (siehe `Money`).
///
/// Der Rechenweg ist absichtlich schlicht: Rest durch Ertrag je Lauf. Was ein
/// Lauf einbringt, kommt aus den eigenen Eintraegen (Truhen je Lauf) und
/// einem Preis - entweder einer Spanne von Hand oder dem Durchschnitt aus der
/// Statistik. Beides sind Schaetzungen, deshalb steht am Ende eine Spanne und
/// keine genaue Zahl.
public static class DebtCalc
{
    /// Was ein Lauf im Mittel einbringt: Truhen je Lauf mal Preis je Truhe.
    public static decimal PerRun(decimal chestsPerRun, decimal pricePerChest) =>
        chestsPerRun <= 0m || pricePerChest <= 0m ? 0m : chestsPerRun * pricePerChest;

    /// Was noch offen ist. Mehr erfarmt als geschuldet ergibt keine negative
    /// Schuld, sondern null.
    public static decimal Rest(decimal debt, decimal farmed) => Math.Max(0m, debt - farmed);

    /// Wie viele Laeufe der Rest kostet, aufgerundet - ein halber Lauf bringt
    /// nichts, man laeuft ihn ganz.
    ///
    /// Ohne Ertrag je Lauf gibt es keine Antwort; dann null, und die Ansicht
    /// sagt, was fehlt.
    public static long Runs(decimal rest, decimal perRun)
    {
        if (rest <= 0m) return 0;
        if (perRun <= 0m) return 0;

        return (long)Math.Ceiling(rest / perRun);
    }

    /// Dasselbe fuer eine Preisspanne. Der hoehere Preis braucht weniger
    /// Laeufe - deshalb steht er in `Min`.
    public static (long Min, long Max) RunsBetween(
        decimal rest, decimal chestsPerRun, decimal priceLow, decimal priceHigh)
    {
        var low = Math.Min(priceLow, priceHigh);
        var high = Math.Max(priceLow, priceHigh);

        return (Runs(rest, PerRun(chestsPerRun, high)),
                Runs(rest, PerRun(chestsPerRun, low)));
    }

    /// Wie lange das dauert, wenn man die Abklingzeit abwartet: Laeufe mal
    /// Abklingzeit. Der erste Lauf wartet nicht, deshalb eine Wartezeit
    /// weniger.
    public static TimeSpan Duration(long runs, int cooldownMinutes)
    {
        if (runs <= 0 || cooldownMinutes <= 0) return TimeSpan.Zero;

        return TimeSpan.FromMinutes((double)(runs - 1) * cooldownMinutes);
    }
}
