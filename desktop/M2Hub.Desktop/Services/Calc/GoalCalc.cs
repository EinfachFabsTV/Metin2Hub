namespace M2Hub.Desktop.Services.Calc;

/// Ziele: was ein Ziel schon eingebracht hat und was noch fehlt.
///
/// Reine Funktionen, wie bei `GuildCalc` und `DebtCalc`. Gerechnet wird hier
/// in **Won**, weil Ziele in Won gesteckt werden („1500w fuer X"); die Laeufe
/// liefern kk, die Umrechnung macht der Aufrufer ueber `Money.KkPerW`.
public static class GoalCalc
{
    /// Was unter dem Strich steht.
    ///
    /// `Runs` ist, was die Laeufe seit dem Start des Ziels eingebracht haben;
    /// `Income` sind Zusatzeinnahmen (ein guenstig gekauftes Item teurer
    /// weiterverkauft), `Expenses` die Ausgaben aller Arten.
    ///
    /// `CarryIn` ist, was vom vorigen Ziel uebrig blieb - falls so eingestellt.
    public static decimal Net(decimal runs, decimal income, decimal expenses, decimal carryIn) =>
        runs + income + carryIn - expenses;

    /// Was ein Shop-Bestand zusaetzlich einbringt.
    ///
    /// `amount` ist, was im Shop liegt (in Won), `counted` der Teil davon, der
    /// ueber die Laeufe schon im Gewinn steckt. Nur der Rest ist neu - sonst
    /// zaehlte dieselbe Truhe zweimal.
    ///
    /// Unter null geht es nicht: wer mehr abzieht, als im Shop liegt, hat sich
    /// vertan, und ein negativer Posten machte daraus stillschweigend eine
    /// Ausgabe.
    public static decimal ShopNet(decimal amount, decimal counted) =>
        Math.Max(0m, amount - counted);

    /// Was noch fehlt. Mehr als das Ziel ergibt keine negative Luecke.
    public static decimal Rest(decimal target, decimal net) => Math.Max(0m, target - net);

    /// Wie weit das Ziel erreicht ist, in Prozent. Ueber hundert wird nicht
    /// hinausgezaehlt - der Balken ist voll, der Ueberschuss steht daneben.
    public static double Progress(decimal target, decimal net)
    {
        if (target <= 0m) return 0;

        return (double)Math.Clamp(net / target * 100m, 0m, 100m);
    }

    /// Was ueber das Ziel hinausging - das, was beim Uebertragen auf das
    /// naechste Ziel zaehlt.
    public static decimal Surplus(decimal target, decimal net) => Math.Max(0m, net - target);

    /// Wie viele Laeufe der Rest noch kostet, aufgerundet. Ohne Ertrag je Lauf
    /// gibt es keine Antwort; dann null, und die Ansicht sagt, was fehlt.
    public static long Runs(decimal rest, decimal perRun)
    {
        if (rest <= 0m || perRun <= 0m) return 0;

        return (long)Math.Ceiling(rest / perRun);
    }
}
