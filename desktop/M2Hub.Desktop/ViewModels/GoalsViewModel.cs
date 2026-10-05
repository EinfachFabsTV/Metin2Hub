using System.Collections.ObjectModel;
using System.Globalization;
using M2Hub.Desktop.Models;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Calc;

namespace M2Hub.Desktop.ViewModels;

/// Ziele: „1500w fuer X", danach „200w fuer Y".
///
/// **Der Gewinn kommt von selbst.** Was die Laeufe seit dem Start des Ziels
/// eingebracht haben, rechnet die App aus den Eintraegen im Run Tracker - ueber
/// alle Laufarten. Von Hand kommen nur die Ausgaben und, als Gegenstueck, die
/// Zusatzeinnahmen.
///
/// **Der aktive Lauf ist Anzeige und Schaetzung**, kein Filter: er steht im
/// Overlay und bestimmt, womit „noch X Laeufe" gerechnet wird.
///
/// Die Ziele stehen als Warteschlange in `goals.json`; das erste offene ist
/// das aktive. Was beim Erreichen geschieht, entscheidet der Nutzer
/// (`GoalsData.OnReached`).
public sealed class GoalsViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly IDialogService _dialogs;
    private readonly StreamOverlay _stream;

    private string _newTitle = "";
    private string _newTarget = "";
    private string _amount = "";
    private string _note = "";
    private string _kind = GoalBooking.Push;
    private RunChip? _chestRun;
    private string _chestCount = "";
    private string _chestPrice = "";

    public GoalsViewModel(LocalStore store, IDialogService dialogs, StreamOverlay stream)
    {
        _store = store;
        _dialogs = dialogs;
        _stream = stream;

        foreach (var run in RunCatalog.Runs)
        {
            Runs.Add(new RunChip(run.Key, run.Name));
            ChestRuns.Add(new RunChip(run.Key, run.Name));
        }

        _chestRun = ChestRuns.FirstOrDefault();

        BuildKinds();
        BuildModes();

        AddGoalCommand = new RelayCommand(_ => AddGoal());
        AddBookingCommand = new RelayCommand(_ => AddBooking());
        AddChestCommand = new RelayCommand(_ => AddChest());
        ShopHelpCommand = new AsyncRelayCommand(_ => _dialogs.ShowAsync(new ShopHelpDialogViewModel()));
        RemoveChestCommand = new RelayCommand(p => RemoveChest(p as GoalChestRowViewModel));
        ChooseKindCommand = new RelayCommand(p => { if (p is RunChip c) Kind = c.Key; });
        ChooseRunCommand = new RelayCommand(p => { if (p is RunChip c) ActiveRun = c.Key; });
        ChooseModeCommand = new RelayCommand(p => { if (p is RunChip c) OnReached = c.Key; });
        FinishCommand = new RelayCommand(_ => Finish());
        DeleteBookingCommand = new AsyncRelayCommand(p => DeleteBookingAsync(p as GoalBookingViewModel));
        DeleteGoalCommand = new AsyncRelayCommand(p => DeleteGoalAsync(p as GoalRowViewModel));
        MoveUpCommand = new RelayCommand(p => Move(p as GoalRowViewModel, -1));
        MoveDownCommand = new RelayCommand(p => Move(p as GoalRowViewModel, +1));

        Reload();
    }

    private GoalsData Data => _store.Goals;

    /* ---------- Das aktive Ziel ---------- */

    /// Das erste offene Ziel der Warteschlange.
    private GoalDto? Active => Data.Goals.Where(g => !g.Done).OrderBy(g => g.Sort).FirstOrDefault();

    public bool HasActive => Active is not null;
    public bool HasNoGoals => Data.Goals.Count == 0;

    public string Title => Active?.Title ?? "—";
    public string TargetLabel => Active is null ? "—" : Won(Active.Target);

    /// Was die Laeufe seit dem Start des Ziels eingebracht haben, in Won.
    private decimal RunProfit(GoalDto goal) =>
        _store.Runs.Entries
            .Where(e => e.AddedAt >= goal.StartedAt)
            .Sum(e => e.Chests * Price(e)) / Money.KkPerW;

    /// Der Preis, mit dem ein Eintrag rechnet - seiner, sonst der des Laufs.
    private decimal Price(RunEntryDto entry)
    {
        if (entry.Price > 0m) return entry.Price;

        return _store.Runs.ChestPrice.TryGetValue(entry.Run, out var p) ? p : RunCatalog.DefaultChestPrice;
    }

    private decimal Expenses(GoalDto goal) =>
        goal.Bookings
            .Where(b => b.Kind != GoalBooking.Income && b.Kind != GoalBooking.Shop)
            .Sum(b => b.Amount);

    /// Zusatzeinnahmen und, was im Shop liegt - letzteres ohne die Truhen,
    /// die ueber die Laeufe schon im Gewinn stehen.
    private decimal Income(GoalDto goal) =>
        goal.Bookings.Where(b => b.Kind == GoalBooking.Income).Sum(b => b.Amount)
        + goal.Bookings.Where(b => b.Kind == GoalBooking.Shop).Sum(ShopNet);

    /// Was ein Shop-Posten unter dem Strich beitraegt.
    private decimal ShopNet(GoalBookingDto booking) =>
        GoalCalc.ShopNet(booking.Amount, Counted(booking));

    /// Der Teil eines Shop-Bestands, der ueber die Laeufe schon gezaehlt ist.
    private decimal Counted(GoalBookingDto booking) =>
        booking.Chests.Sum(c => c.Chests * ChestPrice(c)) / Money.KkPerW;

    /// Der Preis einer Truhenzeile in kk: ihr eigener, sonst der Tagespreis,
    /// den der Run Tracker fuer diesen Lauf zuletzt kennt.
    private decimal ChestPrice(GoalChestDto chest)
    {
        if (chest.Price > 0m) return chest.Price;

        return _store.Runs.ChestPrice.TryGetValue(chest.Run, out var p) ? p : RunCatalog.DefaultChestPrice;
    }

    private decimal Net(GoalDto goal) =>
        GoalCalc.Net(RunProfit(goal), Income(goal), Expenses(goal), goal.CarryIn);

    public string ProfitLabel => Active is null ? "—" : Won(RunProfit(Active));
    public string ExpensesLabel => Active is null ? "—" : Won(Expenses(Active));
    public string IncomeLabel => Active is null ? "—" : Won(Income(Active));
    public string CarryLabel => Active is null ? "—" : Won(Active.CarryIn);
    public bool HasCarry => Active is { CarryIn: > 0m };
    public string NetLabel => Active is null ? "—" : Won(Net(Active));
    public string RestLabel => Active is null ? "—" : Won(GoalCalc.Rest(Active.Target, Net(Active)));

    public double Progress => Active is null ? 0 : GoalCalc.Progress(Active.Target, Net(Active));
    public string ProgressLabel => Progress.ToString("0.#", CultureInfo.CurrentCulture) + " %";

    /// Wie viele Laeufe des aktiven Laufs noch fehlen - geschaetzt aus dem,
    /// was dieser Lauf bisher im Mittel einbrachte.
    public string RunsLeftLabel
    {
        get
        {
            if (Active is null) return "—";

            var rest = GoalCalc.Rest(Active.Target, Net(Active));
            if (rest <= 0m) return Loc.T("goals.reached");

            var perRun = PerRun(Active.ActiveRun);
            if (perRun <= 0m) return "—";

            return Loc.T("goals.runsLeft", GoalCalc.Runs(rest, perRun).ToString("N0", CultureInfo.CurrentCulture));
        }
    }

    /// Was ein Lauf dieser Art im Mittel einbringt, in Won.
    private decimal PerRun(string runKey)
    {
        var mine = _store.Runs.Entries.Where(e => e.Run == runKey).ToList();
        if (mine.Count == 0) return 0m;

        return mine.Sum(e => e.Chests * Price(e)) / mine.Count / Money.KkPerW;
    }

    /* ---------- Der aktive Lauf ---------- */

    public ObservableCollection<RunChip> Runs { get; } = new();

    public string ActiveRun
    {
        get => Active?.ActiveRun ?? "hydra";
        set
        {
            if (Active is null || Active.ActiveRun == value) return;

            Active.ActiveRun = value;
            Save();
        }
    }

    public RelayCommand ChooseRunCommand { get; }

    /* ---------- Ein Ziel anlegen ---------- */

    public string NewTitle { get => _newTitle; set => Set(ref _newTitle, value); }
    public string NewTarget { get => _newTarget; set => Set(ref _newTarget, value); }

    public RelayCommand AddGoalCommand { get; }

    private void AddGoal()
    {
        if (!Read(_newTarget, out var target) || target <= 0m) return;

        Data.Goals.Add(new GoalDto
        {
            Id = Data.TakeId(),
            Title = _newTitle.Trim(),
            Target = target,
            Sort = Data.Goals.Count == 0 ? 0 : Data.Goals.Max(g => g.Sort) + 1,
            StartedAt = DateTime.Now,
            ActiveRun = Active?.ActiveRun ?? "hydra",
        });

        NewTitle = "";
        NewTarget = "";
        Save();
    }

    /* ---------- Ausgaben und Zusatzeinnahmen ---------- */

    /// Die vier Arten als Knopfreihe.
    public ObservableCollection<RunChip> Kinds { get; } = new();

    public string Kind
    {
        get => _kind;
        set
        {
            if (!Set(ref _kind, value)) return;

            foreach (var c in Kinds) c.IsActive = c.Key == _kind;
            Raise(nameof(IsShop));
        }
    }

    public string Amount { get => _amount; set => Set(ref _amount, value); }
    public string Note { get => _note; set => Set(ref _note, value); }

    public RelayCommand ChooseKindCommand { get; }
    public RelayCommand AddBookingCommand { get; }
    public AsyncRelayCommand DeleteBookingCommand { get; }

    public ObservableCollection<GoalBookingViewModel> Bookings { get; } = new();

    /* ---------- Shop-Bestand: die Truhen, die schon zaehlen ---------- */

    /// Nur beim Shop-Bestand steht die Truhenmaske da - bei einer Ausgabe
    /// gaebe es nichts herauszurechnen.
    public bool IsShop => _kind == GoalBooking.Shop;

    /// Alle Laufarten zur Auswahl: in den Gewinn zaehlen sie alle, also kann
    /// auch aus jeder etwas im Shop liegen - nicht nur aus dem aktiven Lauf.
    public ObservableCollection<RunChip> ChestRuns { get; } = new();

    public RunChip? ChestRun { get => _chestRun; set => Set(ref _chestRun, value); }
    public string ChestCount { get => _chestCount; set => Set(ref _chestCount, value); }

    /// Leer heisst: der Tagespreis des Laufs. Verkauft wird aber nicht
    /// zwingend zu dem, deshalb ist er ueberschreibbar.
    public string ChestPriceText { get => _chestPrice; set => Set(ref _chestPrice, value); }

    /// Die Zeilen, die beim naechsten „Eintragen" an den Posten gehen.
    public ObservableCollection<GoalChestRowViewModel> NewChests { get; } = new();

    public bool HasNewChests => NewChests.Count > 0;

    public RelayCommand AddChestCommand { get; }

    /// „Wie sehe ich meinen Shop-Wert?" - vier gezeichnete Schritte.
    public AsyncRelayCommand ShopHelpCommand { get; }
    public RelayCommand RemoveChestCommand { get; }

    private void AddChest()
    {
        if (_chestRun is null) return;
        if (!int.TryParse(_chestCount.Trim(), out var count) || count <= 0) return;

        var price = Read(_chestPrice, out var p) ? p : 0m;

        NewChests.Add(new GoalChestRowViewModel(
            new GoalChestDto { Run = _chestRun.Key, Chests = count, Price = price },
            _chestRun.Label,
            ChestPriceLabel(_chestRun.Key, price)));

        ChestCount = "";
        Raise(nameof(HasNewChests));
        Raise(nameof(NewChestsLabel));
    }

    private void RemoveChest(GoalChestRowViewModel? row)
    {
        if (row is null) return;

        NewChests.Remove(row);
        Raise(nameof(HasNewChests));
        Raise(nameof(NewChestsLabel));
    }

    /// Was von dem Betrag abginge, wenn man jetzt eintruege.
    public string NewChestsLabel
    {
        get
        {
            var counted = NewChests.Sum(r => r.Dto.Chests * ChestPrice(r.Dto)) / Money.KkPerW;

            return Loc.T("goals.shop.counted", Won(counted));
        }
    }

    private string ChestPriceLabel(string run, decimal price)
    {
        var kk = price > 0m
            ? price
            : _store.Runs.ChestPrice.TryGetValue(run, out var p) ? p : RunCatalog.DefaultChestPrice;

        return Money.FormatYang((double)kk) + (price > 0m ? "" : " *");
    }

    private void AddBooking()
    {
        if (Active is null || !Read(_amount, out var amount) || amount <= 0m) return;

        var booking = new GoalBookingDto
        {
            Id = Data.TakeId(),
            Kind = _kind,
            Amount = amount,
            Note = _note.Trim(),
            AddedAt = DateTime.Now,
        };

        // Die Truhen gehoeren zum Posten, nicht zum Ziel: wird er geloescht,
        // verschwindet der Abzug mit ihm.
        if (IsShop) booking.Chests.AddRange(NewChests.Select(r => r.Dto));

        Active.Bookings.Add(booking);

        Amount = "";
        Note = "";
        NewChests.Clear();
        Raise(nameof(HasNewChests));
        Raise(nameof(NewChestsLabel));
        Save();
    }

    private async Task DeleteBookingAsync(GoalBookingViewModel? row)
    {
        if (row is null || Active is null) return;

        var ok = await _dialogs.ConfirmAsync(
            Loc.T("goals.booking.delete"),
            Loc.T("goals.booking.deleteAsk", row.AmountLabel, row.KindLabel));
        if (!ok) return;

        Active.Bookings.RemoveAll(b => b.Id == row.Id);
        Save();
    }

    /* ---------- Die Warteschlange ---------- */

    public ObservableCollection<GoalRowViewModel> Queue { get; } = new();

    public AsyncRelayCommand DeleteGoalCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

    private void Move(GoalRowViewModel? row, int step)
    {
        if (row is null) return;

        var order = Data.Goals.OrderBy(g => g.Sort).ToList();
        var at = order.FindIndex(g => g.Id == row.Id);
        var to = at + step;
        if (at < 0 || to < 0 || to >= order.Count) return;

        (order[at], order[to]) = (order[to], order[at]);
        for (var i = 0; i < order.Count; i++) order[i].Sort = i;
        Save();
    }

    private async Task DeleteGoalAsync(GoalRowViewModel? row)
    {
        if (row is null) return;

        var ok = await _dialogs.ConfirmAsync(
            Loc.T("goals.delete"),
            Loc.T("goals.deleteAsk", row.Title));
        if (!ok) return;

        Data.Goals.RemoveAll(g => g.Id == row.Id);
        Save();
    }

    /* ---------- Was beim Erreichen geschieht ---------- */

    public ObservableCollection<RunChip> Modes { get; } = new();

    public string OnReached
    {
        get => Data.OnReached;
        set
        {
            if (Data.OnReached == value) return;

            Data.OnReached = value;
            foreach (var c in Modes) c.IsActive = c.Key == value;
            Save();
        }
    }

    public RelayCommand ChooseModeCommand { get; }

    /// „Ziel abschliessen" - von Hand, fuer die Betriebsart, die stehen
    /// bleibt, und fuer den Fall, dass man frueher abbrechen will.
    public RelayCommand FinishCommand { get; }

    public bool CanFinish => Active is not null;

    private void Finish()
    {
        if (Active is null) return;

        Close(Active, DateTime.Now);
        Save();
    }

    /// Schliesst ein Ziel ab und bereitet das naechste vor.
    private void Close(GoalDto goal, DateTime at)
    {
        var surplus = GoalCalc.Surplus(goal.Target, Net(goal));

        goal.Done = true;
        goal.DoneAt = at;

        var next = Data.Goals.Where(g => !g.Done).OrderBy(g => g.Sort).FirstOrDefault();
        if (next is null) return;

        // Ab hier zaehlen die Laeufe fuer das naechste Ziel - sonst zaehlte
        // der Abend doppelt.
        next.StartedAt = at;
        next.CarryIn = Data.OnReached == GoalMode.Carry ? surplus : 0m;
    }

    /// Prueft nach jeder Aenderung, ob das aktive Ziel erreicht ist. Bei
    /// „stehen bleiben" passiert nichts - dort schaltet der Nutzer selbst.
    private void Settle()
    {
        if (Data.OnReached == GoalMode.Manual) return;

        // Mehrere auf einmal sind moeglich: wer ein kleines Ziel ueberrennt,
        // soll nicht erst beim naechsten Eintrag weiterruecken.
        for (var guard = 0; guard < Data.Goals.Count; guard++)
        {
            var goal = Active;
            if (goal is null || Net(goal) < goal.Target) return;

            Close(goal, DateTime.Now);
        }
    }

    /* ---------- Nachfuehren ---------- */

    private void Save()
    {
        Settle();
        _store.SaveGoals();
        _stream.Write();
        Reload();
    }

    /// Von aussen: der Run Tracker hat etwas eingetragen.
    public void Reload()
    {
        Settle();

        Queue.Clear();
        foreach (var goal in Data.Goals.OrderBy(g => g.Done).ThenBy(g => g.Sort))
            Queue.Add(new GoalRowViewModel(goal, Won(goal.Target), goal.Id == Active?.Id));

        Bookings.Clear();
        if (Active is not null)
            foreach (var b in Active.Bookings.OrderByDescending(b => b.AddedAt))
                Bookings.Add(new GoalBookingViewModel(
                    b,
                    Won(b.Kind == GoalBooking.Shop ? ShopNet(b) : b.Amount),
                    b.Kind == GoalBooking.Shop && b.Chests.Count > 0
                        ? Loc.T("goals.shop.minus",
                            b.Chests.Sum(c => c.Chests).ToString("N0", CultureInfo.CurrentCulture),
                            Won(Counted(b)))
                        : ""));

        foreach (var c in Runs) c.IsActive = c.Key == ActiveRun;
        foreach (var c in Kinds) c.IsActive = c.Key == _kind;
        foreach (var c in Modes) c.IsActive = c.Key == Data.OnReached;

        Raise(nameof(HasActive));
        Raise(nameof(HasNoGoals));
        Raise(nameof(CanFinish));
        Raise(nameof(Title));
        Raise(nameof(TargetLabel));
        Raise(nameof(ProfitLabel));
        Raise(nameof(ExpensesLabel));
        Raise(nameof(IncomeLabel));
        Raise(nameof(CarryLabel));
        Raise(nameof(HasCarry));
        Raise(nameof(NetLabel));
        Raise(nameof(RestLabel));
        Raise(nameof(Progress));
        Raise(nameof(ProgressLabel));
        Raise(nameof(RunsLeftLabel));
        Raise(nameof(ActiveRun));
        Raise(nameof(HasBookings));
        Raise(nameof(IsShop));
        Raise(nameof(HasNewChests));
        Raise(nameof(NewChestsLabel));
    }

    public bool HasBookings => Bookings.Count > 0;

    /// Nach einem Sprachwechsel.
    public void RelabelAfterLanguageChange()
    {
        BuildKinds();
        BuildModes();
        Reload();
    }

    private void BuildKinds()
    {
        Kinds.Clear();
        Kinds.Add(new RunChip(GoalBooking.Push, Loc.T("goals.kind.push")));
        Kinds.Add(new RunChip(GoalBooking.Extra, Loc.T("goals.kind.extra")));
        Kinds.Add(new RunChip(GoalBooking.Expense, Loc.T("goals.kind.expense")));
        Kinds.Add(new RunChip(GoalBooking.Income, Loc.T("goals.kind.income")));
        Kinds.Add(new RunChip(GoalBooking.Shop, Loc.T("goals.kind.shop")));
        foreach (var c in Kinds) c.IsActive = c.Key == _kind;
    }

    private void BuildModes()
    {
        Modes.Clear();
        Modes.Add(new RunChip(GoalMode.Carry, Loc.T("goals.mode.carry")));
        Modes.Add(new RunChip(GoalMode.Reset, Loc.T("goals.mode.reset")));
        Modes.Add(new RunChip(GoalMode.Manual, Loc.T("goals.mode.manual")));
        foreach (var c in Modes) c.IsActive = c.Key == Data.OnReached;
    }

    /* ---------- Zahlen ---------- */

    internal static string Won(decimal value) =>
        value.ToString("#,0.##", CultureInfo.CurrentCulture) + " " + Loc.T("goals.won");

    /// Wie beim Truhenpreis: Punkt wie Komma.
    private static bool Read(string? text, out decimal value) =>
        decimal.TryParse((text ?? "").Trim().Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0m;
}

/// Die vier Arten eines Postens.
public static class GoalBooking
{
    public const string Push = "push";
    public const string Extra = "extra";
    public const string Expense = "expense";
    public const string Income = "income";

    /// Was im Shop liegt und noch nicht verkauft ist.
    public const string Shop = "shop";

    public static string Label(string kind) => Loc.T("goals.kind." + kind);
}

/// Was beim Erreichen eines Ziels geschieht.
public static class GoalMode
{
    public const string Carry = "carry";
    public const string Reset = "reset";
    public const string Manual = "manual";
}

/// Ein Posten in der Liste.
public sealed class GoalBookingViewModel(GoalBookingDto dto, string amount, string minus = "")
{
    public int Id { get; } = dto.Id;
    public string KindLabel { get; } = GoalBooking.Label(dto.Kind);
    public string AmountLabel { get; } = amount;

    /// Beim Shop-Bestand: was abgezogen wurde, weil es ueber die Laeufe schon
    /// zaehlt. Sonst leer.
    public string MinusLabel { get; } = minus;
    public bool HasMinus { get; } = minus.Length > 0;
    public string Note { get; } = dto.Note;
    public bool HasNote { get; } = dto.Note.Length > 0;
    public string DayLabel { get; } = dto.AddedAt.ToString("dd.MM.");

    /// Zusatzeinnahmen stehen gruen da, Ausgaben in Bernstein - man sieht auf
    /// einen Blick, was dazukam und was abging.
    public bool IsIncome { get; } = dto.Kind is GoalBooking.Income or GoalBooking.Shop;
}

/// Eine Truhenzeile eines Shop-Bestands, so wie sie in der Maske steht.
public sealed class GoalChestRowViewModel(GoalChestDto dto, string run, string price)
{
    public GoalChestDto Dto { get; } = dto;
    public string RunLabel { get; } = run;
    public string ChestsLabel { get; } = dto.Chests.ToString("N0", CultureInfo.CurrentCulture);
    public string PriceLabel { get; } = price;
}

/// Eine Zeile der Warteschlange.
public sealed class GoalRowViewModel(GoalDto dto, string target, bool isActive)
{
    public int Id { get; } = dto.Id;
    public string Title { get; } = dto.Title.Length > 0 ? dto.Title : target;
    public string TargetLabel { get; } = target;
    public bool IsActive { get; } = isActive;
    public bool IsDone { get; } = dto.Done;
}
