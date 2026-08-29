using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace M2Hub.Desktop.Services;

/// Ordnet einem Eventnamen aus dem Forum ein mitgeliefertes Bild zu.
///
/// Die Namen im Eventkalender werden von Hand geschrieben und weichen staendig
/// voneinander ab: Abkuerzungen („Konz. Lesen"), Zusaetze („Elixier der Sonne
/// (M)"), andere Wortwahl („Mondlichtschatzkisten" statt „Mondlicht-
/// Schatztruhe") und schlichte Tippfehler. Ein Vergleich auf Gleichheit findet
/// deshalb fast nichts.
///
/// Darum in vier Stufen, jeweils auf der Vergleichsform (klein, ohne
/// Satzzeichen, Umlaute ausgeschrieben):
///
///   1. exakter Treffer auf Name oder Alias            → 1.00
///   2. der eine Name steckt im anderen                → 0.90
///   3. Wortabgleich: wie viele Woerter passen         → bis 0.95
///      (ein Wort passt auch als Abkuerzung: „konz" → „konzentriertes",
///       und mit Tippfehler: „Segnug" → „Segnung")
///   4. Jaro-Winkler ueber die zusammengezogene Form   → bis 0.93
///
/// Genommen wird der beste Treffer, sofern er ueber der Schwelle liegt.
///
/// Stufe 4 ist doppelt gesichert, sonst zieht sie falsche Treffer an: „Elixier
/// des Lebens" und „Elixier des Mondes" unterscheiden sich fuer den blossen
/// Buchstabenvergleich kaum, meinen aber verschiedene Dinge. Sie greift daher
/// nur ab 0,92 Aehnlichkeit *und* nur, wenn auch die Woerter zueinander passen.
/// Fuellwoerter („des", „der", „B") zaehlen dabei nicht mit - sonst gaebe ein
/// gemeinsames „des" den Ausschlag.
public static partial class EventIcons
{
    /// Ab hier gilt ein Treffer als sicher genug. Lieber kein Bild als ein
    /// falsches - ein falsches Symbol ist im Kalender sofort irrefuehrend.
    private const double Threshold = 0.82;

    /// Ein Bild mit allen Namen, unter denen es im Forum auftauchen kann:
    /// dem deutschen, den Schreibweisen daneben (Aliases) und den Namen aus
    /// den anderen Foren (Localized). Verglichen wird immer gegen alle -
    /// welche Sprache eingestellt ist, spielt fuer die Zuordnung keine Rolle,
    /// und ein deutscher Name im englischen Kalender trifft weiterhin.
    private sealed record Entry(string Name, string File, string[] Aliases, string[] Localized)
    {
        public Entry(string name, string file, string[] aliases)
            : this(name, file, aliases, []) { }
    }

    /// Was mitgeliefert wird. Aliase nur dort, wo im Forum regelmaessig eine
    /// andere Schreibweise auftaucht - Tippfehler faengt die Suche selbst ab.
    ///
    /// Die fremdsprachigen Namen stammen aus den Kalendern selbst: der
    /// tuerkische aus dem Kalender von Marmara/Bağjanamu, der englische aus dem
    /// Thread „Eventi Tigerghost" - das italienische Forum schreibt die Namen
    /// englisch. Was dort nicht vorkam, ist von mir uebersetzt und mit
    /// „uebersetzt" gekennzeichnet; taucht es im Forum anders auf, gehoert es
    /// hier korrigiert. Falsch liegt eine Uebersetzung hoechstens so, dass die
    /// Zelle ohne Bild bleibt - der Vergleich verlangt genuegend Aehnlichkeit.
    private static readonly Entry[] Catalog =
    [
        // Im Kalender taucht dieses Pet als „Robin" auf, die Datei heisst „Bruce"
        new("Bruce", "Bruce.png", ["Robin"], []),
        new("Buch des Anführers", "Buch_des_Anfuehrers.png", ["Anführerbuch"],
            ["Liderin Kitabı", "Leader's Book"]),                     // EN uebersetzt
        new("Cor Draconis", "Cor_Draconis_Roh.png", ["Cor Draconis Roh", "Rohes Cor Draconis"],
            ["Cor Draconis"]),
        new("Elixier der Sonne", "Elixier_der_Sonne_M.png", ["Sonnenelixier"],
            ["Güneş Özütü", "Sun Extract", "Elixir of the Sun"]),     // EN uebersetzt
        new("Elixier des Mondes", "Elixier_des_Mondes_M.png", ["Mondelixier"],
            ["Ay Özütü", "Moon Extract", "Elixir of the Moon"]),      // EN uebersetzt
        new("Exorzismus-Schriftrolle", "Exorzismus_Schriftrolle.png", ["Exo-Schriftrolle", "Exorzismus"],
            ["Kötü Ruh Kovma Kağıdı", "Exorcism Scroll"]),
        new("Feines Tuch", "Feines_Tuch.png", [],
            ["Fine Cloth", "İnce Kumaş"]),                            // TR uebersetzt
        new("Fischpuzzle", "Fischpuzzel.png", ["Fischpuzzlespiel", "Fisch-Puzzle", "Puzzlespiel"],
            ["Balıkçılık", "Fishing Event", "Fishing"]),
        new("Flamme des Drachen", "Flamme_des_Drachen.png", ["Drachenflamme"],
            ["Flame of the Dragon", "Ejderha Alevi"]),                // TR uebersetzt
        new("Gegenstand verstärken B", "Gegenstand_verstaerken_B.png", ["Verstärken B", "Gegenstand verstärken"],
            ["Arttırma Kağıdı", "Reinforce Item B", "Reinforce Item"]),
        new("Gegenstand verzaubern B", "Gegenstand_verzaubern_B.png", ["Verzaubern B", "Gegenstand verzaubern"],
            ["Nesneyi Efsunla", "Enchant Item B", "Enchant Item"]),
        new("Grüne Drachenbohne", "Gruene_Drachenbohne.png", ["Drachenbohne"],
            ["Yeşil Ejderha Fasülyesi", "Green Dragon Bean"]),
        new("Inventarerweiterung", "Inventarerweiterung.png", ["Inventar-Erweiterung"],
            ["Inventory Expansion", "Envanter Genişletme"]),          // TR uebersetzt
        new("Kleine Segnung", "Kleine_Segnung.png", [],
            ["Küçük Kutsama", "Small Orison"]),
        // Im tuerkischen Kalender steht an dieser Stelle „Münzevi Tavsiyesi";
        // die Zuordnung folgt dem Platz im Kalender, nicht dem Wortlaut.
        new("Konzentriertes Lesen", "Konzentriertes_Lesen.png", ["Konz. Lesen", "Konz Lesen"],
            ["Münzevi Tavsiyesi", "Concentrated Reading"]),
        new("Mondlicht-Schatztruhe", "Mondlicht_Schatztruhe.png", ["Mondlichtschatzkisten", "Mondlicht-Schatzkiste"],
            ["Ay Işığı", "Moonlight"]),
        // „Nugget (grün)" im Kalender - der Klammerzusatz faellt beim Vergleich weg
        new("Muffin grün", "Muffin_gruen.png", ["Grüner Muffin", "Nugget", "Nuggit", "Nugget grün"],
            ["Nugget"]),
        new("Passierschein", "Passierschein.png", [],
            ["Passage Ticket", "Geçiş Bileti"]),                      // TR uebersetzt
        new("Pet-Bücherkiste", "Pet_Buecherkiste.png", ["Pet Buch Kiste", "Pet-Buchkiste"],
            ["Pet Book Chest", "Evcil Hayvan Kitap Sandığı"]),        // TR uebersetzt
        new("Purpur-Ebenholzkasten", "Purpur_Ebenholzkasten.png", ["Ebenholzkasten"],
            ["Crimson Ebony Box", "Kızıl Abanoz Kutusu"]),            // TR uebersetzt
        new("Segenskugel", "Segenskugel.png", [],
            ["Kutsama Küresi", "Blessing Marble"]),
        new("Segensschriftrolle", "Segensschriftrolle.png", ["Segens-Schriftrolle"],
            ["Kutsama Kağıdı", "Blessing Scroll"]),
        new("Supersteine", "Supersteine.jpg", ["Superstein"],
            ["Süper Taş", "Superstone"]),
        new("Teleportationsring", "Teleportationsring.png", ["Telering", "Teleportring"],
            ["Teleportation Ring", "Işınlanma Yüzüğü"]),              // TR uebersetzt
    ];

    /// Name und Alias jeweils in Vergleichsform, mit Verweis auf den Eintrag.
    private static readonly List<(string Key, string Compact, string[] Words, Entry Entry)> Keys = Build();

    private static List<(string, string, string[], Entry)> Build()
    {
        var list = new List<(string, string, string[], Entry)>();
        foreach (var entry in Catalog)
        {
            foreach (var raw in new[] { entry.Name }.Concat(entry.Aliases).Concat(entry.Localized))
            {
                var key = Normalize(raw);
                if (key.Length == 0) continue;
                list.Add((key, key.Replace(" ", ""), key.Split(' '), entry));
            }
        }
        return list;
    }

    [GeneratedRegex(@"\([^)]*\)")] private static partial Regex Parenthesis();
    [GeneratedRegex(@"[^a-z0-9]+")] private static partial Regex NotWord();

    /// Vergleichsform: klein, Umlaute ausgeschrieben, ohne Satzzeichen und
    /// ohne Klammerzusatz („Nugget (grün)" → „nugget").
    public static string Normalize(string? text)
    {
        // Das tuerkische İ wird sonst zu „i̇" mit Punkt daneben.
        var value = (text ?? "").Replace("İ", "i").ToLowerInvariant();
        value = Parenthesis().Replace(value, " ");

        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case 'ä': sb.Append("ae"); break;
                case 'ö': sb.Append("oe"); break;
                case 'ü': sb.Append("ue"); break;
                case 'ß': sb.Append("ss"); break;

                // Tuerkisch und Italienisch: diese Zeichen wuerden sonst als
                // Satzzeichen gelten und ein Wort mitten entzweischneiden
                // („ağustos" wuerde zu „a ustos").
                case 'ı': case 'î': case 'ì': case 'í': sb.Append('i'); break;
                case 'ğ': sb.Append('g'); break;
                case 'ş': sb.Append('s'); break;
                case 'ç': sb.Append('c'); break;
                case 'à': case 'á': case 'â': sb.Append('a'); break;
                case 'è': case 'é': case 'ê': sb.Append('e'); break;
                case 'ò': case 'ó': case 'ô': sb.Append('o'); break;
                case 'ù': case 'ú': case 'û': sb.Append('u'); break;

                default: sb.Append(c); break;
            }
        }

        return NotWord().Replace(sb.ToString(), " ").Trim();
    }

    /// Bildpfad zu einem Eventnamen, oder null wenn nichts sicher genug passt.
    public static string? FindFile(string? text)
    {
        var query = Normalize(text);
        if (query.Length < 3) return null;

        var compact = query.Replace(" ", "");
        var words = query.Split(' ');

        Entry? best = null;
        var bestScore = 0.0;

        foreach (var (key, keyCompact, keyWords, entry) in Keys)
        {
            var score = Score(query, compact, words, key, keyCompact, keyWords);
            if (score > bestScore)
            {
                bestScore = score;
                best = entry;
            }
        }

        return bestScore >= Threshold ? best!.File : null;
    }

    private static double Score(
        string query, string compact, string[] words,
        string key, string keyCompact, string[] keyWords)
    {
        if (query == key) return 1.0;
        if (compact == keyCompact) return 0.98;

        // Der kuerzere Name steckt vollstaendig im laengeren
        if (compact.Length >= 5 && keyCompact.Length >= 5 &&
            (compact.Contains(keyCompact, StringComparison.Ordinal) ||
             keyCompact.Contains(compact, StringComparison.Ordinal)))
            return 0.90;

        var cover = Coverage(words, keyWords);
        var score = cover * 0.95;

        // Buchstabenweg nur bei sehr aehnlichen Namen, und nur wenn auch die
        // Woerter oder ein langer gemeinsamer Anfang dafuer sprechen.
        var letters = JaroWinkler(compact, keyCompact);
        if (letters >= 0.92 && Math.Max(cover, PrefixShare(compact, keyCompact)) >= 0.6)
            score = Math.Max(score, letters * 0.93);

        return score;
    }

    /// Fuellwoerter tragen nichts zur Unterscheidung bei.
    private static readonly HashSet<string> Filler = new(StringComparer.Ordinal)
    {
        "der", "die", "das", "des", "dem", "den", "im", "in", "von", "vom",
        "zu", "zur", "und", "a", "b", "m", "x",
    };

    private static string[] Content(string[] words)
    {
        var rest = words.Where(w => !Filler.Contains(w)).ToArray();
        return rest.Length > 0 ? rest : words;
    }

    /// Anteil der Woerter des Eintrags, die im gesuchten Text vorkommen -
    /// gemessen an der laengeren der beiden Seiten, damit ein einzelnes Wort
    /// nicht auf jeden mehrteiligen Namen passt.
    private static double Coverage(string[] words, string[] keyWords)
    {
        var q = Content(words);
        var k = Content(keyWords);
        if (k.Length == 0 || q.Length == 0) return 0;

        var matched = 0.0;
        foreach (var kw in k)
        {
            var best = 0.0;
            foreach (var w in q)
            {
                if (w == kw) { best = 1.0; break; }
                // Abkuerzung: „konz" → „konzentriertes"
                if (w.Length >= 3 && kw.Length >= 3 &&
                    (kw.StartsWith(w, StringComparison.Ordinal) || w.StartsWith(kw, StringComparison.Ordinal)))
                    best = Math.Max(best, 0.9);
                // Tippfehler innerhalb eines Wortes
                else if (w.Length >= 4 && kw.Length >= 4 && JaroWinkler(w, kw) >= 0.88)
                    best = Math.Max(best, 0.8);
            }
            matched += best;
        }

        return matched / Math.Max(k.Length, q.Length);
    }

    /// Anteil des gemeinsamen Wortanfangs - trennt „Mondlichtschatzkisten"
    /// (gleicher Anfang) von „Kleine Kiste" (nichts gemeinsam).
    private static double PrefixShare(string a, string b)
    {
        var n = 0;
        var max = Math.Min(a.Length, b.Length);
        while (n < max && a[n] == b[n]) n++;
        return n / (double)Math.Max(a.Length, b.Length);
    }

    /* ---------- Jaro-Winkler ---------- */
    // Gewichtet gemeinsame Anfaenge staerker - genau das, was bei
    // „Mondlichtschatz…" gegen „Mondlicht-Schatztruhe" gebraucht wird.

    public static double JaroWinkler(string a, string b)
    {
        var jaro = Jaro(a, b);
        if (jaro < 0.7) return jaro;

        var prefix = 0;
        var max = Math.Min(4, Math.Min(a.Length, b.Length));
        while (prefix < max && a[prefix] == b[prefix]) prefix++;

        return jaro + prefix * 0.1 * (1 - jaro);
    }

    private static double Jaro(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;

        var window = Math.Max(a.Length, b.Length) / 2 - 1;
        if (window < 0) window = 0;

        var aFlags = new bool[a.Length];
        var bFlags = new bool[b.Length];
        var matches = 0;

        for (var i = 0; i < a.Length; i++)
        {
            var from = Math.Max(0, i - window);
            var to = Math.Min(b.Length - 1, i + window);
            for (var j = from; j <= to; j++)
            {
                if (bFlags[j] || a[i] != b[j]) continue;
                aFlags[i] = true;
                bFlags[j] = true;
                matches++;
                break;
            }
        }
        if (matches == 0) return 0;

        // Vertauschte Zeichen zaehlen halb
        var transpositions = 0;
        var k = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (!aFlags[i]) continue;
            while (!bFlags[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }

        double m = matches;
        return (m / a.Length + m / b.Length + (m - transpositions / 2.0) / m) / 3.0;
    }

    /* ---------- Bilder ---------- */

    private static readonly Dictionary<string, Bitmap?> Loaded = new(StringComparer.Ordinal);

    /// Passendes Bild zu einem Eventnamen. Die Bilder liegen als Ressource in
    /// der Exe; geladen wird jedes hoechstens einmal.
    public static Bitmap? Find(string? text)
    {
        var file = FindFile(text);
        if (file is null) return null;

        if (Loaded.TryGetValue(file, out var cached)) return cached;

        Bitmap? bitmap = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://M2Hub/Assets/Events/{file}"));
            bitmap = new Bitmap(stream);
        }
        catch
        {
            // Fehlende oder kaputte Ressource: dann eben ohne Bild.
        }

        Loaded[file] = bitmap;
        return bitmap;
    }
}
