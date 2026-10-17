namespace M2Hub.Desktop.Services.Calc;

/// Rechenweg fuer das Okey-Minispiel - reine Funktionen, ohne Framework.
///
/// Regeln (bestaetigt): Ein Kartenset = 24 Karten, **genau je eine pro
/// Zahl×Farbe** (8 Zahlen 1-8 × 3 Farben). Man sieht 5 Karten offen, wertet
/// eine 3er-Kombo (Set = 3 gleiche Zahlen, oder Reihe = 3 aufeinanderfolgende
/// Zahlen, gleich- oder gemischtfarbig), zieht nach und spielt so, bis der
/// Stapel leer ist. Verworfene Karten sind verbraucht. Die Punkte summieren
/// sich; die Gesamtsumme bestimmt die Truhe: ab 400 Gold, ab 300 Silber.
public static class OkeyCalc
{
    public const int Gold = 400;
    public const int Silver = 300;

    public const int Numbers = 8;   // 1..8
    public const int Colors = 3;    // 0=Rot, 1=Blau, 2=Gelb

    public const int KindInvalid = -1;
    public const int KindSet = 0;        // drei gleiche Zahlen
    public const int KindRowSame = 1;    // Reihe, eine Farbe
    public const int KindRowMixed = 2;   // Reihe, gemischte Farben

    /// Alle Zahlen als Set werten - verwurffrei moeglich und schon Gold.
    public const int AllSets = 440;

    /// Theoretischer Bestwert bei voller Information (hohe Zahlen als
    /// gleichfarbige Reihen, Rest als Sets): 3×100 + 3×70 + 20 + 30 = 560.
    public const int TheoreticalMax = 560;

    /// Punkte einer 3er-Kombo, 0 = ungueltig.
    public static int Score(OkeyCard a, OkeyCard b, OkeyCard c) => Eval(a, b, c).Score;

    /// Punkte und Art einer 3er-Kombo.
    public static (int Score, int Kind) Eval(OkeyCard a, OkeyCard b, OkeyCard c)
    {
        if (a.Number > b.Number) (a, b) = (b, a);
        if (b.Number > c.Number) (b, c) = (c, b);
        if (a.Number > b.Number) (a, b) = (b, a);

        if (a.Number == b.Number && b.Number == c.Number)
            return (a.Color != b.Color && b.Color != c.Color && a.Color != c.Color)
                ? (10 * (a.Number + 1), KindSet)
                : (0, KindInvalid);

        if (b.Number == a.Number + 1 && c.Number == b.Number + 1)
        {
            var same = a.Color == b.Color && b.Color == c.Color;
            return same ? (40 + 10 * a.Number, KindRowSame) : (10 * a.Number, KindRowMixed);
        }

        return (0, KindInvalid);
    }

    /// Die hoechstwertige gueltige Kombo aus der Hand (Indizes + Punkte + Art).
    /// Score 0 heisst: keine gueltige Kombo.
    public static (int A, int B, int C, int Score, int Kind) BestCombo(IReadOnlyList<OkeyCard> hand)
    {
        int bi = -1, bj = -1, bk = -1, bs = 0, bkind = KindInvalid;
        for (var i = 0; i < hand.Count; i++)
            for (var j = i + 1; j < hand.Count; j++)
                for (var k = j + 1; k < hand.Count; k++)
                {
                    var (s, kind) = Eval(hand[i], hand[j], hand[k]);
                    if (s > bs) { bi = i; bj = j; bk = k; bs = s; bkind = kind; }
                }
        return (bi, bj, bk, bs, bkind);
    }

    /// Alle gueltigen Kombos der Hand, nach Punkten absteigend.
    public static List<OkeyCombo> AllCombos(IReadOnlyList<OkeyCard> hand)
    {
        var list = new List<OkeyCombo>();
        for (var i = 0; i < hand.Count; i++)
            for (var j = i + 1; j < hand.Count; j++)
                for (var k = j + 1; k < hand.Count; k++)
                {
                    var (s, kind) = Eval(hand[i], hand[j], hand[k]);
                    if (s > 0) list.Add(new OkeyCombo(hand[i], hand[j], hand[k], s, kind));
                }
        list.Sort((x, y) => y.Score.CompareTo(x.Score));
        return list;
    }

    /// Wert der hoechsten Kombo, die sich aus diesen Karten noch bilden laesst
    /// (0 = keine). Dient als Obergrenze: mit `count/3` Kombos ist mehr nicht
    /// drin.
    public static int BestSingleComboValue(IReadOnlyList<OkeyCard> cards)
    {
        var best = 0;
        for (var i = 0; i < cards.Count; i++)
            for (var j = i + 1; j < cards.Count; j++)
                for (var k = j + 1; k < cards.Count; k++)
                    best = Math.Max(best, Score(cards[i], cards[j], cards[k]));
        return best;
    }

    /* ---------------- Wegwerf-Empfehlung ---------------- */

    /// Index der Handkarte, die man am ehesten wegwerfen sollte - die mit der
    /// geringsten noch erreichbaren Kombo (`present` = Karte noch im Spiel,
    /// also nicht verbraucht). So bleibt die beste Punktchance offen.
    public static int BestDiscardIndex(IReadOnlyList<OkeyCard> hand, bool[,] present)
    {
        var worst = 0;
        var worstVal = int.MaxValue;
        for (var i = 0; i < hand.Count; i++)
        {
            var v = KeepValue(hand[i], present);
            if (v < worstVal) { worstVal = v; worst = i; }
        }
        return worst;
    }

    /// Die hoechste Kombo, zu der die Karte noch beitragen koennte, wenn die
    /// noetigen Partnerkarten noch im Spiel sind.
    public static int KeepValue(OkeyCard x, bool[,] present)
    {
        var best = 0;
        int n = x.Number, c = x.Color;

        // Set: die anderen beiden Farben derselben Zahl noch da?
        var setOk = true;
        for (var cc = 0; cc < Colors; cc++)
            if (cc != c && !At(present, n, cc)) setOk = false;
        if (setOk) best = Math.Max(best, 10 * (n + 1));

        // Reihen: Fenster, die n enthalten
        for (var s = n - 2; s <= n; s++)
        {
            if (s < 1 || s > 6) continue;
            // die beiden anderen Zahlen des Fensters (ohne n) bestimmen
            var o1 = -1; var o2 = -1;
            for (var num = s; num <= s + 2; num++)
            {
                if (num == n) continue;
                if (o1 < 0) o1 = num; else o2 = num;
            }

            // gleiche Farbe
            if (At(present, o1, c) && At(present, o2, c))
                best = Math.Max(best, 40 + 10 * s);

            // gemischt (jede andere Zahl in irgendeiner Farbe)
            if (AnyColor(present, o1) && AnyColor(present, o2))
                best = Math.Max(best, 10 * s);
        }

        return best;
    }

    private static bool At(bool[,] present, int number, int color) =>
        number >= 1 && number <= Numbers && color >= 0 && color < Colors && present[number - 1, color];

    private static bool AnyColor(bool[,] present, int number)
    {
        for (var c = 0; c < Colors; c++) if (At(present, number, c)) return true;
        return false;
    }

    /* ---------------- Monte-Carlo ---------------- */

    /// Spielt `runs` Durchlaeufe ab dem aktuellen Stand (Punkte, Hand,
    /// Restdeck) und gibt die Verteilung des Endstands zurueck. Starke
    /// Heuristik (Sets und gleichfarbige Reihen bevorzugt, wenig Verwurf), kein
    /// beweisbares Optimum.
    public static OkeyStats Simulate(
        int runs, int startPoints, IReadOnlyList<OkeyCard> startHand, IReadOnlyList<OkeyCard> startDeck,
        int? seed = null)
    {
        var rng = seed is { } s ? new Random(s) : new Random();
        long sum = 0;
        int gold = 0, silver = 0, bronze = 0, min = int.MaxValue, max = 0;

        for (var r = 0; r < runs; r++)
        {
            var deck = new List<OkeyCard>(startDeck);
            Shuffle(deck, rng);
            var hand = new List<OkeyCard>(startHand);
            DrawUpTo(hand, deck, 5);

            var total = PlayFrom(startPoints, hand, deck);
            sum += total;
            if (total >= Gold) gold++;
            else if (total >= Silver) silver++;
            else bronze++;
            if (total < min) min = total;
            if (total > max) max = total;
        }

        if (runs <= 0) return new OkeyStats(0, startPoints, 0, 0, 0, startPoints, startPoints);
        return new OkeyStats(runs, sum / (double)runs,
            gold / (double)runs, silver / (double)runs, bronze / (double)runs, min, max);
    }

    /// Die vollen 24 Karten eines frischen Sets.
    public static List<OkeyCard> FullDeck()
    {
        var deck = new List<OkeyCard>(Numbers * Colors);
        for (var n = 1; n <= Numbers; n++)
            for (var c = 0; c < Colors; c++)
                deck.Add(new OkeyCard(n, c));
        return deck;
    }

    private static int PlayFrom(int startPoints, List<OkeyCard> hand, List<OkeyCard> deck)
    {
        var total = startPoints;
        var stall = 0;

        while (true)
        {
            var best = BestCombo(hand);
            var good = best.Score > 0 && (best.Kind == KindSet || best.Kind == KindRowSame);

            if (good)
            {
                total += best.Score;
                RemoveThree(hand, best.A, best.B, best.C);
                DrawUpTo(hand, deck, 5);
                stall = 0;
                continue;
            }

            if (deck.Count > 0 && stall < 2)
            {
                var worst = LeastUseful(hand);
                hand.RemoveAt(worst);
                hand.Add(deck[^1]);
                deck.RemoveAt(deck.Count - 1);
                stall++;
                continue;
            }

            if (best.Score > 0)
            {
                total += best.Score;
                RemoveThree(hand, best.A, best.B, best.C);
                DrawUpTo(hand, deck, 5);
                stall = 0;
                continue;
            }

            break;
        }

        return total;
    }

    private static void Shuffle(List<OkeyCard> deck, Random rng)
    {
        for (var i = deck.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    private static void DrawUpTo(List<OkeyCard> hand, List<OkeyCard> deck, int target)
    {
        while (hand.Count < target && deck.Count > 0)
        {
            hand.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
        }
    }

    private static void RemoveThree(List<OkeyCard> hand, int a, int b, int c)
    {
        var idx = new[] { a, b, c };
        Array.Sort(idx);
        hand.RemoveAt(idx[2]);
        hand.RemoveAt(idx[1]);
        hand.RemoveAt(idx[0]);
    }

    private static int LeastUseful(List<OkeyCard> hand)
    {
        var worst = 0;
        var worstScore = int.MaxValue;
        for (var i = 0; i < hand.Count; i++)
        {
            var partners = 0;
            for (var j = 0; j < hand.Count; j++)
            {
                if (i == j) continue;
                var dn = Math.Abs(hand[i].Number - hand[j].Number);
                if (dn == 0 || dn <= 2) partners++;
            }
            if (partners < worstScore) { worstScore = partners; worst = i; }
        }
        return worst;
    }
}

/// Eine Karte: Zahl 1-8, Farbe 0=Rot, 1=Blau, 2=Gelb.
public readonly record struct OkeyCard(int Number, int Color);

/// Eine gueltige Kombo fuer den Live-Helfer.
public sealed record OkeyCombo(OkeyCard A, OkeyCard B, OkeyCard C, int Score, int Kind);

/// Ergebnis der Simulation.
public sealed record OkeyStats(
    int Runs, double Mean, double PGold, double PSilver, double PBronze, int Min, int Max);
