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

    private RunChip _run;
    private DateTime _day = DateTime.Today;
    private int _chests;
    private string _scope = "day";
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
        StartTimerCommand = new RelayCommand(_ => StartTimer());
        StopTimerCommand = new RelayCommand(_ => StopTimer());

        // Der Wecker laeuft nur, solange eine Abklingzeit laeuft.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();

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
            Raise(nameof(DayLabel));
            RaiseStats();
        }
    }

    public string DayLabel => _day.ToString("dd.MM.yyyy");
    public RelayCommand TodayCommand { get; }

    /* ---------- Eintragen ---------- */

    /// Erhaltene Truhen. Bei Jotun gibt es das Feld nicht - dort zaehlt die
    /// Beute darunter.
    public int Chests { get => _chests; set => Set(ref _chests, Math.Max(0, value)); }

    public bool EntersChests => Current.EntersChests;
    public string AddLabel => Loc.T(EntersChests ? "runs.add" : "runs.finish");
    public string AddHint => Loc.T(EntersChests ? "runs.chests" : "runs.finishHint");

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
        StartTimer();
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
            Raise(nameof(Scope));
            RaiseStats();
        }
    }

    public RelayCommand ChooseScopeCommand { get; }

    public string ScopeLabel => Scope.Label;

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
            "month" => mine.Where(e => e.Day.StartsWith(_day.ToString("yyyy-MM"), StringComparison.Ordinal)),
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

    /// Preis je Truhe in Won. Steht je Lauf und laesst sich hier aendern -
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

    /// Truhen mal Preis, in Won.
    public string Income => (InScope().Sum(e => e.Chests) * (long)ChestPrice).ToString("N0") + " " + Loc.T("runs.won");

    public string PriceNote => Loc.T("runs.priceNote", RunCatalog.YangPerWon.ToString("N0"));

    /// Die Beute des Zeitraums, aufgeschluesselt.
    public ObservableCollection<LootSumViewModel> LootSums { get; } = new();

    /* ---------- Insgesamt ---------- */

    private IEnumerable<RunEntryDto> Mine() => _store.Runs.Entries.Where(e => e.Run == _run.Key);

    public string TotalRuns => Mine().Count().ToString("N0");
    public string TotalChests => Mine().Sum(e => e.Chests).ToString("N0");

    public string TotalAverage
    {
        get
        {
            var list = Mine().ToList();
            return list.Count == 0 ? "0,00" : ((double)list.Sum(e => e.Chests) / list.Count).ToString("N2");
        }
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

    public ObservableCollection<RunEntryViewModel> Entries { get; } = new();

    public bool HasEntries => Entries.Count > 0;

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

        // Eintraege, neueste oben
        Entries.Clear();
        foreach (var e in Mine().OrderByDescending(e => e.Day).ThenByDescending(e => e.AddedAt))
            Entries.Add(new RunEntryViewModel(e));

        ResetTimer();

        Raise(nameof(Subtitle));
        Raise(nameof(RunName));
        Raise(nameof(EntersChests));
        Raise(nameof(AddLabel));
        Raise(nameof(AddHint));
        Raise(nameof(ChestPrice));
        Raise(nameof(Cooldown));
        Raise(nameof(HasEntries));
        RaiseStats();
    }

    private void RaiseStats()
    {
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
