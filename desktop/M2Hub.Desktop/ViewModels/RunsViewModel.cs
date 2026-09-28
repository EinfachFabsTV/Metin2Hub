using System.Collections.ObjectModel;
using Avalonia.Threading;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.ViewModels;

/// Run Tracker: eingetragene Laeufe je Tag, mit Ausbeute und Ertrag.
///
/// Uebernommen aus m2tracker.de. Alles liegt lokal in runs.json - wie bei den
/// Accounts gibt es kein Konto und keinen Server. Export und Import braucht es
/// deshalb nicht: die Datei ist die Sicherung, und der Ordner steht in den
/// Einstellungen.
public sealed class RunsViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _midnight;
    private DateTime _today = DateTime.Today;

    private RunChip _run;
    private DateTime _day = DateTime.Today;
    private int _chests;
    private string _scope = "day";
    private string _month = DateTime.Today.ToString("yyyy-MM");
    private string _monthFilter = "";
    private TimeSpan _left;
    private bool _running;

    public RunsViewModel(LocalStore store, IDialogService dialogs)
    {
        _store = store;
        _dialogs = dialogs;

        foreach (var r in RunCatalog.Runs) Runs.Add(new RunChip(r.Key, r.Name));
        _run = Runs[0];
        _run.IsActive = true;

        BuildScopes();

        ChooseRunCommand = new RelayCommand(p => { if (p is RunChip o) Run = o; });
        ChooseScopeCommand = new RelayCommand(p => { if (p is RunChip o) Scope = o; });
        AddCommand = new RelayCommand(_ => Add());
        TodayCommand = new RelayCommand(_ => Day = DateTime.Today);
        DeleteCommand = new AsyncRelayCommand(p => DeleteAsync(p as RunEntryViewModel));
        ClearCommand = new AsyncRelayCommand(_ => ClearAsync());
        QuickCommand = new RelayCommand(p => { if (p is QuickChest q) Chests = q.Value; });
        StartTimerCommand = new RelayCommand(_ => StartTimer());
        StopTimerCommand = new RelayCommand(_ => StopTimer());

        // Der Wecker laeuft nur, solange eine Abklingzeit laeuft.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();

        // Um Mitternacht faengt der naechste Tag an. Stand der Waehler auf
        // dem alten heute, geht er mit - sonst traegt man nach Mitternacht
        // weiter auf gestern ein und die Kacheln zaehlen den Vortag mit.
        _midnight = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _midnight.Tick += (_, _) => CheckDay();
        _midnight.Start();

        Picker = new DayPickerViewModel(_day, d => Day = d);
        // Monatsname und Wochentage stehen in der eingestellten Sprache.
        Loc.I.PropertyChanged += (_, _) =>
        {
            Picker.Refresh();
            BuildMonths();
        };

        Reload();
    }

    /* ---------- Auswahl ---------- */

    public ObservableCollection<RunChip> Runs { get; } = new();

    public RunChip Run
    {
        get => _run;
        set
        {
            if (value is null || !Set(ref _run, value)) return;
            foreach (var o in Runs) o.IsActive = o.Key == _run.Key;
            StopTimer();
            Reload();
        }
    }

    public RelayCommand ChooseRunCommand { get; }

    private RunCatalog.Run Current => RunCatalog.Find(_run.Key) ?? RunCatalog.Runs[0];

    public string Subtitle => RunCatalog.Subtitle(Current);
    public string RunName => Current.Name;

    /* ---------- Tag ---------- */

    /// Der Tag, auf den eingetragen wird. Die Statistik „heute" rechnet
    /// dagegen immer auf den heutigen Tag - sonst hiesse die Zeile anders.
    public DateTime Day
    {
        get => _day;
        set
        {
            if (!Set(ref _day, value.Date)) return;
            Picker.Select(_day);
            Raise(nameof(DayLabel));
            RaiseStats();
        }
    }

    /// Das Raster der Tage - eigener Waehler statt des Fluent-Kalenders,
    /// damit die Karte aussieht wie die uebrigen.
    public DayPickerViewModel Picker { get; }

    public string DayLabel => _day.ToString("dd.MM.yyyy");
    public RelayCommand TodayCommand { get; }

    /* ---------- Eintragen ---------- */

    /// Erhaltene Truhen. Bei Jotun gibt es das Feld nicht - dort zaehlt die
    /// Beute darunter.
    public int Chests
    {
        get => _chests;
        set
        {
            if (!Set(ref _chests, Math.Max(0, value))) return;
            foreach (var q in Quick) q.IsActive = q.Value == _chests;
        }
    }

    /// Schnellwahl unter dem Feld: 0 bis zur groessten Zahl des Laufs. Bei der
    /// Hydra sind es hoechstens fuenf Truhen, das trifft man mit einem Klick;
    /// wo acht bis zehn oder vierundsechzig fallen, gibt es keine.
    public ObservableCollection<QuickChest> Quick { get; } = new();
    public bool HasQuick => EntersChests && Quick.Count > 0;
    public RelayCommand QuickCommand { get; }

    public bool EntersChests => Current.EntersChests;
    public string AddLabel => Loc.T(EntersChests ? "runs.add" : "runs.finish");
    public string AddHint => Loc.T(EntersChests ? "runs.chests" : "runs.finishHint");

    /// Das Bild der Truhe neben dem Eingabefeld - sonst steht dort als
    /// einzige Zeile ohne Bild, was gezaehlt wird.
    public Avalonia.Media.Imaging.Bitmap? ChestIcon => EventIcons.Find(Current.ChestName);
    public bool HasChestIcon => EntersChests && ChestIcon is not null;

    /// Die Beutearten des Laufs, je mit ihrem Zaehler.
    public ObservableCollection<LootRowViewModel> Loot { get; } = new();

    public RelayCommand AddCommand { get; }

    private void Add()
    {
        var loot = Loot
            .Where(r => r.Count > 0)
            .ToDictionary(r => r.Name, r => r.Count);

        // Ohne eigenes Truhenfeld ergibt die Beute die Truhenzahl.
        var chests = EntersChests ? _chests : loot.Values.Sum();
        if (chests == 0 && loot.Count == 0) return;

        _store.Runs.Entries.Add(new RunEntryDto
        {
            Id = _store.Runs.TakeId(),
            Run = _run.Key,
            Day = _day.ToString("yyyy-MM-dd"),
            Chests = chests,
            Loot = loot,
            AddedAt = DateTime.Now,
        });
        _store.SaveRuns();

        Chests = 0;
        foreach (var row in Loot) row.Count = 0;

        Reload();
    }

    /* ---------- Statistik ---------- */

    public ObservableCollection<RunChip> Scopes { get; } = new();

    /// Tag, Monat oder alles - die drei Reiter ueber der Statistik.
    public RunChip Scope
    {
        get => Scopes.FirstOrDefault(o => o.Key == _scope) ?? Scopes[0];
        set
        {
            if (value is null || !Set(ref _scope, value.Key)) return;
            foreach (var o in Scopes) o.IsActive = o.Key == _scope;

            // Beim Umschalten auf den Monat den des gewaehlten Tages zeigen -
            // das ist der Monat, den man gerade vor sich hat.
            if (_scope == "month") _month = _day.ToString("yyyy-MM");

            Raise(nameof(Scope));
            Raise(nameof(IsMonthScope));
            Raise(nameof(Month));
            RaiseStats();
        }
    }

    public RelayCommand ChooseScopeCommand { get; }

    public string ScopeLabel => Scope.Label;

    /* ---------- Monatsauswahl ---------- */

    /// Die Monate, aus denen gewaehlt werden kann: der laufende und jeder, in
    /// dem fuer diesen Lauf etwas steht, neueste zuerst.
    ///
    /// Frueher zeigte die Monatsstatistik immer den Monat des gewaehlten
    /// Tages. Weil `runs.json` nie verworfen wird, waechst der Vorrat aber
    /// ueber Jahre - ohne Auswahl kaeme man nur ueber den Kalender an einen
    /// aelteren Monat.
    public ObservableCollection<MonthOption> Months { get; } = new();

    public MonthOption? Month
    {
        get => Months.FirstOrDefault(m => m.Key == _month) ?? Months.FirstOrDefault();
        set
        {
            // Die Auswahl mit Suche liefert beim Tippen zwischendurch null.
            if (value is null || !Set(ref _month, value.Key)) return;
            Raise(nameof(Month));
            RaiseStats();
        }
    }

    private bool Matches(MonthOption month) =>
        _monthFilter.Length == 0
        || month.Label.Contains(_monthFilter, StringComparison.CurrentCultureIgnoreCase)
        || month.Key.Contains(_monthFilter, StringComparison.Ordinal);

    /// Nur bei der Monatsstatistik steht die Auswahl da.
    public bool IsMonthScope => _scope == "month";

    /// Das Suchwort ueber der Liste. Getippt wird gefiltert; der gewaehlte
    /// Monat bleibt immer darin, sonst faende die Auswahl ihn nicht wieder.
    public string MonthFilter
    {
        get => _monthFilter;
        set { if (Set(ref _monthFilter, value ?? "")) BuildMonths(); }
    }

    private void BuildMonths()
    {
        var months = Mine()
            .Select(e => e.Day.Length >= 7 ? e.Day[..7] : null)
            .Where(k => k is not null)
            .Append(DateTime.Today.ToString("yyyy-MM"))
            .Append(_month)
            .Distinct()
            .OrderByDescending(k => k, StringComparer.Ordinal)
            .Select(k => new MonthOption(k!))
            .Where(m => m.Key == _month || Matches(m))
            .ToList();

        Months.Clear();
        foreach (var month in months) Months.Add(month);
        Raise(nameof(Month));
    }

    private void BuildScopes()
    {
        var chosen = _scope;
        Scopes.Clear();
        Scopes.Add(new RunChip("day", Loc.T("runs.scope.day")));
        Scopes.Add(new RunChip("month", Loc.T("runs.scope.month")));
        Scopes.Add(new RunChip("all", Loc.T("runs.scope.all")));
        foreach (var o in Scopes) o.IsActive = o.Key == chosen;
    }

    private IEnumerable<RunEntryDto> InScope()
    {
        var mine = _store.Runs.Entries.Where(e => e.Run == _run.Key);
        return _scope switch
        {
            "day" => mine.Where(e => e.Day == _day.ToString("yyyy-MM-dd")),
            "month" => mine.Where(e => e.Day.StartsWith(_month, StringComparison.Ordinal)),
            _ => mine,
        };
    }

    public string RunCount => InScope().Count().ToString("N0");
    public string ChestCount => InScope().Sum(e => e.Chests).ToString("N0");

    public string ChestAverage
    {
        get
        {
            var list = InScope().ToList();
            return list.Count == 0 ? "0,00" : ((double)list.Sum(e => e.Chests) / list.Count).ToString("N2");
        }
    }

    /// Preis je Truhe **in kk**. Steht je Lauf und laesst sich hier aendern -
    /// was die Truhe wert ist, weiss nur der Nutzer.
    public int ChestPrice
    {
        get => _store.Runs.ChestPrice.TryGetValue(_run.Key, out var p) ? p : RunCatalog.DefaultChestPrice;
        set
        {
            var price = Math.Max(0, value);
            if (ChestPrice == price) return;

            _store.Runs.ChestPrice[_run.Key] = price;
            _store.SaveRuns();
            Raise(nameof(ChestPrice));
            Raise(nameof(Income));
        }
    }

    /// Truhen mal Preis. Gerechnet in kk, geschrieben ab 100 kk in w - die
    /// Umrechnung macht `Money.FormatYang` fuer die ganze App.
    public string Income => Money.FormatYang(InScope().Sum(e => e.Chests) * (double)ChestPrice);

    public string PriceNote => Loc.T("runs.priceNote", Money.KkPerW);

    /// Die Beute des Zeitraums, aufgeschluesselt.
    public ObservableCollection<LootSumViewModel> LootSums { get; } = new();

    /* ---------- Der Tag ---------- */

    private IEnumerable<RunEntryDto> Mine() => _store.Runs.Entries.Where(e => e.Run == _run.Key);

    /// Die Eintraege des gewaehlten Tages. Die Kacheln zaehlten frueher alles
    /// zusammen, was je eingetragen wurde - am naechsten Abend stand dort
    /// immer noch die Summe des Vortages. Was ueber laengere Zeit
    /// zusammenkommt, steht in der Statistik daneben (Monat, komplett).
    private IEnumerable<RunEntryDto> OfDay() =>
        Mine().Where(e => e.Day == _day.ToString("yyyy-MM-dd"));

    public string TotalRuns => OfDay().Count().ToString("N0");
    public string TotalChests => OfDay().Sum(e => e.Chests).ToString("N0");

    public string TotalAverage
    {
        get
        {
            var list = OfDay().ToList();
            return list.Count == 0 ? "0,00" : ((double)list.Sum(e => e.Chests) / list.Count).ToString("N2");
        }
    }

    /// Prueft, ob seit dem letzten Blick ein neuer Tag begonnen hat.
    private void CheckDay()
    {
        var today = DateTime.Today;
        if (today == _today) return;

        var followed = _day == _today;
        _today = today;
        if (followed) Day = today;
    }

    /* ---------- Abklingzeit ---------- */

    /// Abklingzeit in Minuten. Die Vorgabe steht im Katalog, geaendert wird
    /// sie hier.
    public int Cooldown
    {
        get => _store.Runs.Cooldown.TryGetValue(_run.Key, out var m) ? m : Current.CooldownMinutes;
        set
        {
            var minutes = Math.Clamp(value, 0, 24 * 60);
            if (Cooldown == minutes) return;

            _store.Runs.Cooldown[_run.Key] = minutes;
            _store.SaveRuns();
            Raise(nameof(Cooldown));
            if (!_running) ResetTimer();
        }
    }

    public string TimerLabel => $"{(int)_left.TotalMinutes:00}:{_left.Seconds:00}";
    public bool TimerRunning { get => _running; private set { if (Set(ref _running, value)) Raise(nameof(TimerIdle)); } }
    public bool TimerIdle => !_running;

    public RelayCommand StartTimerCommand { get; }
    public RelayCommand StopTimerCommand { get; }

    private void StartTimer()
    {
        if (Cooldown <= 0) return;

        _left = TimeSpan.FromMinutes(Cooldown);
        TimerRunning = true;
        Raise(nameof(TimerLabel));
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer.Stop();
        TimerRunning = false;
        ResetTimer();
    }

    private void ResetTimer()
    {
        _left = TimeSpan.FromMinutes(Cooldown);
        Raise(nameof(TimerLabel));
    }

    private void Tick()
    {
        _left -= TimeSpan.FromSeconds(1);
        if (_left <= TimeSpan.Zero)
        {
            _left = TimeSpan.Zero;
            _timer.Stop();
            TimerRunning = false;
        }
        Raise(nameof(TimerLabel));
    }

    /* ---------- Eintraege ---------- */

    /// Die Eintraege, nach Tagen gebuendelt.
    public ObservableCollection<RunDayViewModel> Days { get; } = new();

    public bool HasEntries => Days.Count > 0;

    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand ClearCommand { get; }

    private async Task DeleteAsync(RunEntryViewModel? entry)
    {
        if (entry is null) return;

        var ok = await _dialogs.ConfirmAsync(
            Loc.T("runs.delete"),
            Loc.T("runs.delete.ask", entry.DayLabel, entry.Chests));
        if (!ok) return;

        _store.Runs.Entries.RemoveAll(e => e.Id == entry.Id);
        _store.SaveRuns();
        Reload();
    }

    private async Task ClearAsync()
    {
        var count = Mine().Count();
        if (count == 0) return;

        var ok = await _dialogs.ConfirmAsync(
            Loc.T("runs.clear"),
            Loc.T("runs.clear.ask", count, RunName));
        if (!ok) return;

        _store.Runs.Entries.RemoveAll(e => e.Run == _run.Key);
        _store.SaveRuns();
        Reload();
    }

    /* ---------- Aufbauen ---------- */

    public void Reload()
    {
        // Beutezeilen des gewaehlten Laufs
        var keep = Loot.ToDictionary(r => r.Name, r => r.Count);
        Loot.Clear();
        foreach (var name in Current.Loot)
            Loot.Add(new LootRowViewModel(name, keep.TryGetValue(name, out var c) ? c : 0));

        BuildDays();

        // Die Monate, aus denen gewaehlt werden kann - je gewaehltem Lauf.
        BuildMonths();

        // Welche Tage im Raster gruen stehen - die des gewaehlten Laufs.
        Picker.Mark(Mine().Select(e => e.Day));

        // Schnellwahl des Laufs
        Quick.Clear();
        foreach (var value in Current.Quick) Quick.Add(new QuickChest(value) { IsActive = value == _chests });

        ResetTimer();

        Raise(nameof(HasQuick));
        Raise(nameof(Subtitle));
        Raise(nameof(RunName));
        Raise(nameof(EntersChests));
        Raise(nameof(AddLabel));
        Raise(nameof(AddHint));
        Raise(nameof(ChestIcon));
        Raise(nameof(HasChestIcon));
        Raise(nameof(ChestPrice));
        Raise(nameof(Cooldown));
        Raise(nameof(HasEntries));
        RaiseStats();
    }

    /// Die Liste der Tage. Sie folgt dem gewaehlten Zeitraum - bei
        /// „Tagesstatistik" steht nur der eine Tag da, bei „komplett" alles.
    /// Die Liste der Tage. Sie folgt dem gewaehlten Zeitraum - bei
    /// „Tagesstatistik" steht nur der eine Tag da, bei „komplett" alles.
    ///
    /// Eine Zeile je Lauf wurde nach wenigen Wochen unlesbar; jetzt steht je
    /// Tag eine Zeile mit der Summe, die sich aufklappen laesst.
    private void BuildDays()
    {
        // Aufgeklappte Tage bleiben aufgeklappt.
        var open = Days.Where(d => d.IsOpen).Select(d => d.Key).ToHashSet();

        Days.Clear();
        var groups = InScope()
            .GroupBy(e => e.Day)
            .OrderByDescending(g => g.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var group in groups)
        {
            // Zugeklappt, bis jemand sie aufklappt. Von selbst aufzugehen
            // hiesse, dass die Kachel beim Eintragen jedes Mal aufspringt.
            Days.Add(new RunDayViewModel(group.Key, group.OrderByDescending(e => e.AddedAt))
            {
                IsOpen = open.Contains(group.Key),
            });
        }

        Raise(nameof(HasEntries));
    }

    private void RaiseStats()
    {
        BuildDays();

        LootSums.Clear();
        var sums = new Dictionary<string, int>();
        foreach (var e in InScope())
            foreach (var (name, count) in e.Loot)
                sums[name] = sums.TryGetValue(name, out var had) ? had + count : count;

        // Die Reihenfolge des Katalogs, danach was sonst noch vorkam.
        foreach (var name in Current.Loot)
            LootSums.Add(new LootSumViewModel(name, sums.TryGetValue(name, out var c) ? c : 0));
        foreach (var (name, count) in sums)
            if (!Current.Loot.Contains(name)) LootSums.Add(new LootSumViewModel(name, count));

        Raise(nameof(RunCount));
        Raise(nameof(ChestCount));
        Raise(nameof(ChestAverage));
        Raise(nameof(Income));
        Raise(nameof(ScopeLabel));
        Raise(nameof(TotalRuns));
        Raise(nameof(TotalChests));
        Raise(nameof(TotalAverage));
    }

    /// Nach einem Sprachwechsel die festen Beschriftungen neu setzen.
    public void RelabelAfterLanguageChange()
    {
        BuildScopes();
        Raise(nameof(Scope));
        Raise(nameof(PriceNote));
        Reload();
    }
}

/// Ein Knopf in einer Auswahlreihe: Lauf oder Zeitraum. Traegt neben der
/// Beschriftung, ob er gerade gewaehlt ist - daran haengt die Hervorhebung.
/// Ein Monat in der Auswahl. Der Schluessel ist sprachneutral (yyyy-MM), die
/// Beschriftung folgt der eingestellten Sprache.
public sealed class MonthOption(string key)
{
    public string Key { get; } = key;

    public string Label { get; } = DateTime.TryParseExact(
        key, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.None, out var date)
            ? date.ToString("MMMM yyyy", Culture())
            : key;

    /// Wie beim Tageswaehler: die Sprache der Oberflaeche, nicht die von
    /// Windows.
    private static System.Globalization.CultureInfo Culture()
    {
        try { return System.Globalization.CultureInfo.GetCultureInfo(Services.Loc.I.Language); }
        catch (System.Globalization.CultureNotFoundException)
        { return System.Globalization.CultureInfo.CurrentCulture; }
    }

    /// Die Auswahl mit Suche vergleicht ueber den Text.
    public override string ToString() => Label;
}

/// Ein Knopf der Schnellwahl: eine feste Truhenzahl.
public sealed class QuickChest(int value) : ViewModelBase
{
    private bool _isActive;

    public int Value { get; } = value;
    public string Label { get; } = value.ToString();

    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
}

public sealed class RunChip(string key, string label) : ViewModelBase
{
    private bool _isActive;

    public string Key { get; } = key;
    public string Label { get; } = label;

    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
}

/// Eine Beutezeile beim Eintragen: Name und Zaehler mit − und +.
public sealed class LootRowViewModel : ViewModelBase
{
    private int _count;

    public LootRowViewModel(string name, int count)
    {
        Name = name;
        _count = count;
        Icon = EventIcons.Find(name);
        MoreCommand = new RelayCommand(_ => Count++);
        LessCommand = new RelayCommand(_ => Count--);
    }

    public string Name { get; }
    public Avalonia.Media.Imaging.Bitmap? Icon { get; }
    public bool HasIcon => Icon is not null;

    public int Count { get => _count; set => Set(ref _count, Math.Max(0, value)); }

    public RelayCommand MoreCommand { get; }
    public RelayCommand LessCommand { get; }
}

/// Eine Zeile der Beute-Aufschluesselung in der Statistik.
public sealed class LootSumViewModel
{
    public LootSumViewModel(string name, int count)
    {
        Name = name;
        Count = count.ToString("N0");
        Icon = EventIcons.Find(name);
    }

    public string Name { get; }
    public string Count { get; }
    public Avalonia.Media.Imaging.Bitmap? Icon { get; }
    public bool HasIcon => Icon is not null;
}

/// Ein eingetragener Lauf in der Liste unten.
/// Ein Tag in der Liste: die Summe in der Zeile, die einzelnen Laeufe
/// darunter, sobald man ihn aufklappt.
public sealed class RunDayViewModel : ViewModelBase
{
    private bool _isOpen;

    public RunDayViewModel(string key, IEnumerable<RunEntryDto> entries)
    {
        Key = key;
        DayLabel = DateTime.TryParse(key, out var day) ? day.ToString("dd.MM.yyyy") : key;

        foreach (var e in entries) Entries.Add(new RunEntryViewModel(e));

        Summary = Loc.T("runs.day.summary", Entries.Count, Entries.Sum(e => e.Chests));
        ToggleCommand = new RelayCommand(_ => IsOpen = !IsOpen);
    }

    public string Key { get; }
    public string DayLabel { get; }
    public string Summary { get; }
    public ObservableCollection<RunEntryViewModel> Entries { get; } = new();

    public bool IsOpen
    {
        get => _isOpen;
        set { if (Set(ref _isOpen, value)) Raise(nameof(Chevron)); }
    }

    /// Das Zeichen am Zeilenende sagt, ob der Tag offen steht.
    public string Chevron => _isOpen ? "▾" : "▸";

    public RelayCommand ToggleCommand { get; }
}

public sealed class RunEntryViewModel
{
    public RunEntryViewModel(RunEntryDto dto)
    {
        Id = dto.Id;
        Chests = dto.Chests;
        DayLabel = DateTime.TryParse(dto.Day, out var day)
            ? day.ToString("dd.MM.yyyy")
            : dto.Day;

        ChestLabel = Loc.T("runs.entry.chests", dto.Chests);
        LootLabel = string.Join(" · ", dto.Loot.Where(p => p.Value > 0).Select(p => $"{p.Value}× {p.Key}"));
        HasLoot = LootLabel.Length > 0;
    }

    public int Id { get; }
    public int Chests { get; }
    public string DayLabel { get; }
    public string ChestLabel { get; }
    public string LootLabel { get; }
    public bool HasLoot { get; }
}
