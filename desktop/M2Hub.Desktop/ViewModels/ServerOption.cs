using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Ein Server zur Auswahl - in den Einstellungen, im Filter der Accounts und
/// in der Account-Maske.
///
/// Welche Server es gibt, steht nicht fest: sie stammen aus den geladenen
/// Kalendern und unterscheiden sich je Sprache und Monat. Deshalb wird die
/// Liste aufgebaut statt verdrahtet.
public sealed class ServerOption : ViewModelBase
{
    private string _label;

    public ServerOption(string key, string label)
    {
        Key = key;
        _label = label;
    }

    public string Key { get; }

    public string Label { get => _label; set => Set(ref _label, value); }

    /// Der Platzhalter („keiner", „alle") traegt keinen Schluessel.
    public bool IsRealServer => Key.Length > 0;
}

public static class ServerCatalog
{
    /// Kalender, die sich mehrere Server teilen.
    ///
    /// Im Forum steht fuer Chimera, Oceana und Blos ein gemeinsamer Beitrag -
    /// die Events sind dieselben. Gespielt wird trotzdem auf drei getrennten
    /// Servern, und die Accounts liegen je auf einem davon. Deshalb zwei
    /// Sichten: Calendars() fuer die Event-Reiter, GameServers() fuer alles,
    /// was mit Accounts zu tun hat.
    ///
    /// Erkannt wird die Gruppe am Inhalt des Schluessels, nicht an seinem
    /// genauen Wortlaut: der entsteht aus der Ueberschrift im Forum und
    /// aendert sich, sobald dort ein Server dazukommt.
    private static readonly (string[] Needles, (string Key, string Label)[] Members)[] Groups =
    [
        (["chimera", "oceana"],
         [("chimera", "[Ruby]Chimera"),
          ("oceana", "[SAPPHIRE]Oceana"),
          ("blos", "[DIAMOND]Blos")]),
    ];

    private static (string Key, string Label)[]? MembersOf(string calendarKey)
    {
        foreach (var (needles, members) in Groups)
            if (needles.All(n => calendarKey.Contains(n, StringComparison.OrdinalIgnoreCase)))
                return members;
        return null;
    }

    /// Die Kalender, so wie sie im Forum stehen - ein Reiter je Beitrag.
    public static List<ServerOption> Calendars(LocalStore store)
    {
        var list = new List<ServerOption>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var server in store.Cache.Servers)
            if (seen.Add(server.Key))
                list.Add(new ServerOption(server.Key, server.Label));

        return list;
    }

    /// Die Server, auf denen gespielt wird - dahin gehoeren die Accounts.
    ///
    /// Fest eingetragen, nicht aus dem Kalender abgeleitet: der Kalender fasst
    /// mehrere Server zu einem Beitrag zusammen (Chimera, Oceana und Blos
    /// teilen sich einen), und er nennt nur die, fuer die gerade etwas
    /// ansteht. Beides taugt nicht als Serverliste.
    private static readonly (string Key, string Label)[] KnownGameServers =
    [
        ("tigerghost", "Tigerghost"),
        ("chimera", "[Ruby]Chimera"),
        ("lucifer", "[Ruby]Lucifer"),
        ("charon", "[Ruby]Charon"),
        ("oceana", "[SAPPHIRE]Oceana"),
        ("safir", "[SAPPHIRE]Safir"),
        ("star", "[SAPPHIRE]Star"),
        ("blos", "[DIAMOND]Blos"),
        ("nite", "[DIAMOND]Nite"),
    ];

    /// Schluessel zu einem selbst eingetragenen Servernamen.
    public static string CustomKey(string label) =>
        "eigen-" + Services.Forum.EventCalendar.Slug(label);

    /// Die bekannten Server, dazu die selbst eingetragenen.
    ///
    /// Zuordnungen auf einen Kalender, der mehrere Server umfasst, tauchen hier
    /// bewusst nicht auf: „[Ruby]Chimera / [SAPPHIRE]Oceana" ist kein Server,
    /// auf dem ein Account liegen kann.
    public static List<ServerOption> GameServers(LocalStore store)
    {
        var list = new List<ServerOption>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (key, label) in KnownGameServers)
            if (seen.Add(key)) list.Add(new ServerOption(key, label));

        foreach (var label in store.Accounts.CustomServers)
        {
            var name = label.Trim();
            if (name.Length == 0) continue;

            var key = CustomKey(name);
            if (seen.Add(key)) list.Add(new ServerOption(key, name));
        }

        // Zuordnungen aus frueheren Versionen, die weder bekannt noch selbst
        // eingetragen sind - sie stehen weiter zur Wahl, statt still zu
        // verschwinden. Gruppen bleiben aussen vor, die sind kein Server.
        foreach (var account in store.Accounts.Accounts)
        {
            if (account.ServerKey.Length == 0 || MembersOf(account.ServerKey) is not null) continue;
            if (!seen.Add(account.ServerKey)) continue;

            var label = account.ServerLabel.Length > 0 ? account.ServerLabel : account.ServerKey;
            list.Add(new ServerOption(account.ServerKey, label));
        }

        return list;
    }

    /// Loest Zuordnungen auf einen gemeinsamen Kalender.
    ///
    /// Bis Version 1.16 liess sich ein Account „[Ruby]Chimera / [SAPPHIRE]Oceana"
    /// zuordnen - das sind zwei Server, kein Ort. Solche Zuordnungen werden
    /// einmalig geloest; welcher der Server gemeint war, kann nur der Nutzer
    /// sagen. Der Account steht danach ohne Server da und laesst sich neu
    /// zuordnen.
    public static bool DropGroupAssignments(LocalStore store)
    {
        var changed = false;
        foreach (var account in store.Accounts.Accounts)
        {
            if (account.ServerKey.Length == 0 || MembersOf(account.ServerKey) is null) continue;

            account.ServerKey = "";
            account.ServerLabel = "";
            changed = true;
        }
        return changed;
    }

    /// Zu welchem Kalender ein Spielserver gehoert - fuer die Frage, ob er
    /// ausgeblendet ist.
    private static bool InHiddenGroup(LocalStore store, string gameServerKey)
    {
        foreach (var (_, members) in Groups)
        {
            if (!members.Any(m => m.Key == gameServerKey)) continue;

            // Der Kalender dieser Gruppe steht unter seinem eigenen Schluessel
            // in den Einstellungen - gesucht wird er ueber dieselben Woerter.
            foreach (var hidden in store.Settings.HiddenServers)
                if (MembersOf(hidden) is not null) return true;
        }
        return false;
    }

    /// Server, die der Nutzer nicht sehen moechte. Ausgeblendet wird der
    /// Kalender; die Server darin gelten damit ebenfalls als ausgeblendet.
    public static bool IsHidden(LocalStore store, string key) =>
        store.Settings.HiddenServers.Contains(key, StringComparer.Ordinal) ||
        InHiddenGroup(store, key);
}

/// Ein Lauf zur Auswahl im Filter: Meley, Balathor, Serpent, Grotte - oder
/// der Platzhalter ohne Schluessel fuer „alle".
public sealed class RunOption(string key, string label)
{
    public string Key { get; } = key;
    public string Label { get; } = label;
}
