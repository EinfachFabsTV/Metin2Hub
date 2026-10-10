using System.Collections.ObjectModel;
using Avalonia.Threading;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.ViewModels;

/// Die Abklingzeiten der Setups - ein Knopf je Setup.
///
/// **Je Setup ein Timer, nicht je Laufart.** Wer zwei Hydra-Chars hat, laeuft
/// abwechselnd und braucht zwei Uhren; wie viele es je Lauf sind, steht in
/// den Einstellungen (`TimersData.Counts`). Dazu kommen selbst angelegte
/// Timer fuer alles, was zu keinem Lauf gehoert.
///
/// **Ein Klick startet, der naechste haelt an** - mehr tut der Knopf nicht.
/// Der Lauf wird weiterhin im Run Tracker eingetragen: ein Klick, der
/// nebenbei bucht, traegt frueher oder spaeter etwas ein, das nicht stimmt.
///
/// Die Uhr laeuft im ViewModel, nicht im Fenster: das Knopffenster laesst
/// sich schliessen, ohne dass die Abklingzeiten verlorengehen.
public sealed class RunTimersViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly StreamOverlay _stream;
    private readonly DispatcherTimer _tick;

    private readonly Action _entered;

    public RunTimersViewModel(LocalStore store, StreamOverlay stream, Action entered)
    {
        _store = store;
        _stream = stream;
        _entered = entered;

        EnterCommand = new RelayCommand(p => Enter(p as ChestButton));

        StartCommand = new RelayCommand(p => Toggle(p as RunTimerViewModel));
        StopCommand = new RelayCommand(p => Stop(p as RunTimerViewModel));

        Build();

        // Sekundentakt: die Knoepfe zaehlen sichtbar herunter, und die
        // Dateien fuer OBS sollen nicht hinterherhinken.
        _tick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tick.Tick += (_, _) => Update();
        _tick.Start();
    }

    private TimersData Data => _store.Settings.Timers;

    public ObservableCollection<RunTimerViewModel> Timers { get; } = new();

    public bool HasTimers => Timers.Count > 0;

    /* ---------- Schnelleingabe ---------- */

    /// Je eingerichtetem Lauf eine Zeile mit den Truhenzahlen, die er
    /// ueblicherweise abwirft (`RunCatalog.Run.Entry`). Ein Klick traegt den
    /// Lauf **sofort** ein, mit dieser Zahl und dem heutigen Datum - kein
    /// Feld, keine Tastatur: im Spiel hat man die Hand an der Maus.
    public ObservableCollection<ChestRow> ChestRows { get; } = new();

    /// Nur wenn eingeschaltet und ueberhaupt ein Lauf eingerichtet ist.
    public bool HasChestRows => Data.QuickEntry && ChestRows.Count > 0;

    public RelayCommand EnterCommand { get; }

    private void Enter(ChestButton? button)
    {
        if (button is null) return;

        var run = RunCatalog.Find(button.Run);
        if (run is null) return;

        var day = DateTime.Today.ToString("yyyy-MM-dd");

        _store.Runs.Entries.Add(new RunEntryDto
        {
            Id = _store.Runs.TakeId(),
            Run = run.Key,
            Day = day,
            Chests = button.Chests,
            Price = PriceOfDay(run.Key, day),
            AddedAt = DateTime.Now,
        });
        _store.SaveRuns();

        // Der Run Tracker, die Startseite und die Einblendung zeigen danach
        // dasselbe wie dieses Fenster.
        _entered();
    }

    /// Der Preis, mit dem der Eintrag rechnet: der des heutigen Tages, sonst
    /// der zuletzt hinterlegte des Laufs. Wie im Run Tracker - der Preis
    /// gehoert zum Tag.
    private decimal PriceOfDay(string run, string day)
    {
        var today = _store.Runs.Entries
            .Where(e => e.Run == run && e.Day == day && e.Price > 0m)
            .Select(e => e.Price)
            .ToList();

        if (today.Count > 0) return today[^1];

        return _store.Runs.ChestPrice.TryGetValue(run, out var p) && p > 0m
            ? p
            : RunCatalog.DefaultChestPrice;
    }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    /// Baut die Knoepfe aus den Einstellungen neu. **Laufende Uhren bleiben
    /// stehen**: wer waehrend des Spielens einen Timer dazunimmt, soll die
    /// anderen nicht verlieren.
    public void Build()
    {
        var running = Timers.Where(t => t.EndsAt is not null)
            .ToDictionary(t => t.Key, t => t.EndsAt);

        Timers.Clear();

        foreach (var run in RunCatalog.Runs)
        {
            var count = Data.Counts.TryGetValue(run.Key, out var n) ? n : 0;

            for (var i = 1; i <= count; i++)
            {
                // Bei nur einem Setup waere „Hydra 1" eine Zahl ohne Zweck.
                var label = count > 1 ? $"{run.Name} {i}" : run.Name;

                Add($"{run.Key}#{i}", label, run.CooldownMinutes * 60, running);
            }
        }

        foreach (var own in Data.Custom)
        {
            var label = own.Name.Length > 0 ? own.Name : Loc.T("timers.custom");

            Add($"custom#{own.Id}", label, Math.Max(1, own.Seconds), running);
        }

        BuildChestRows();

        Raise(nameof(HasTimers));
        Raise(nameof(HasChestRows));
        Update();
    }

    /// Eine Zeile je Lauf, fuer den Timer eingerichtet sind - was keinen
    /// Knopf oben hat, braucht auch keinen unten.
    private void BuildChestRows()
    {
        ChestRows.Clear();
        if (!Data.QuickEntry) return;

        foreach (var run in RunCatalog.Runs)
        {
            if (!Data.Counts.TryGetValue(run.Key, out var count) || count <= 0) continue;
            if (run.Entry.Length == 0) continue;

            ChestRows.Add(new ChestRow(run.Name, run.Entry.Select(n => new ChestButton(run.Key, n))));
        }
    }

    private void Add(string key, string label, int seconds, Dictionary<string, DateTime?> running)
    {
        var timer = new RunTimerViewModel(key, label, seconds);
        if (running.TryGetValue(key, out var ends)) timer.EndsAt = ends;

        Timers.Add(timer);
    }

    /// **Ein Klick schaltet um**: steht die Uhr, laeuft sie los; laeuft sie,
    /// haelt sie an. Im Spiel trifft man einen Knopf, nicht zwei - und wer
    /// den Lauf abbricht, will dieselbe Flaeche treffen wie beim Starten.
    private void Toggle(RunTimerViewModel? timer)
    {
        if (timer is null) return;

        timer.EndsAt = timer.EndsAt is null ? DateTime.Now.AddSeconds(timer.Seconds) : null;
        Update();
    }

    /// Anhalten, ohne umzuschalten - fuer den Rechtsklick, der auch dann
    /// zurueckstellt, wenn gerade nichts laeuft.
    private void Stop(RunTimerViewModel? timer)
    {
        if (timer is null) return;

        timer.EndsAt = null;
        Update();
    }

    /// Einmal je Sekunde: Knoepfe nachfuehren und die Dateien schreiben.
    private void Update()
    {
        var now = DateTime.Now;
        var done = false;

        foreach (var timer in Timers) done |= timer.Refresh(now);

        // Einmal, auch wenn zwei Uhren im selben Takt ablaufen - zwei Toene
        // uebereinander klaengen nach Fehler.
        if (done && Data.Sound) Sound.Timer(Data.SoundName);

        // Ohne Timer gibt es nichts zu schreiben - sonst legte die App
        // Dateien an, die niemand bestellt hat.
        if (Data.WriteFiles && Timers.Count > 0)
            _stream.WriteTimers(Timers.Select(t => (t.Key, t.Label, t.Text, t.IsRunning)).ToList());
    }

    /// Nach einem Sprachwechsel - die Namen stehen als feste Zeichenketten.
    public void RelabelAfterLanguageChange() => Build();
}

/// Ein Knopf: Name, Abklingzeit, Restzeit.
public sealed class RunTimerViewModel(string key, string label, int seconds) : ViewModelBase
{
    private string _text = "";
    private bool _running;

    public string Key { get; } = key;
    public string Label { get; } = label;

    /// Die volle Abklingzeit in Sekunden.
    public int Seconds { get; } = seconds;

    /// Wann sie ablaeuft. Null heisst: steht still.
    public DateTime? EndsAt { get; set; }

    /// „19:59", oder die volle Zeit, solange nichts laeuft - ein leerer Knopf
    /// saehe aus, als waere er kaputt.
    public string Text { get => _text; private set => Set(ref _text, value); }

    public bool IsRunning { get => _running; private set => Set(ref _running, value); }

    /// Liefert true, wenn die Uhr **in diesem Takt** abgelaufen ist - dann
    /// gibt es einen Ton.
    public bool Refresh(DateTime now)
    {
        if (EndsAt is not { } ends)
        {
            IsRunning = false;
            Text = Format(Seconds);
            return false;
        }

        var left = (int)Math.Ceiling((ends - now).TotalSeconds);
        if (left <= 0)
        {
            // Abgelaufen heisst bereit: die Uhr faellt weg, der Knopf zeigt
            // wieder die volle Zeit und laedt zum naechsten Lauf ein.
            EndsAt = null;
            IsRunning = false;
            Text = Format(Seconds);
            return true;
        }

        IsRunning = true;
        Text = Format(left);

        return false;
    }

    /// „05:30", ab einer Stunde „1:05:30" - laenger laeuft kein Lauf, aber
    /// ein eigener Timer darf es.
    private static string Format(int seconds) =>
        seconds >= 3600
            ? $"{seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}"
            : $"{seconds / 60:00}:{seconds % 60:00}";
}

/// Eine Zeile der Schnelleingabe: der Name des Laufs und seine Knoepfe.
public sealed class ChestRow(string run, IEnumerable<ChestButton> buttons)
{
    public string Run { get; } = run;
    public IReadOnlyList<ChestButton> Buttons { get; } = buttons.ToList();
}

/// Ein Knopf der Schnelleingabe: „8" traegt acht Truhen dieses Laufs ein.
public sealed class ChestButton(string run, int chests)
{
    public string Run { get; } = run;
    public int Chests { get; } = chests;
    public string Label { get; } = chests.ToString();
}
