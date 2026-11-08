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
/// alle Laufarten. Von Hand kommen nur Ausgaben, Zusatzeinnahmen und der
/// Won-Abgleich mit dem Spiel.
///
/// Die Ziele stehen als Warteschlange in `goals.json`; das erste offene ist
/// das aktive. Was beim Erreichen geschieht, entscheidet der Nutzer je Ziel
/// (`GoalDto.OnReached`).
public sealed class GoalsViewModel : ViewModelBase
{
    private readonly LocalStore _store;
    private readonly IDialogService _dialogs;
    private readonly StreamOverlay _stream;

    private string _newTitle = "";
    private string _newTarget = "";
    private string _amount = "";
    private string _note = "";
    private string _kind = GoalBooking.Income;
    private string _setCurrentWon = "";

    /// Beim Bearbeiten gemerkte Buchung: der naechste „Eintragen"-Klick
    /// ueberschreibt sie, statt eine neue anzulegen.
    private int? _editId;
    private int _editGoalId;

    public GoalsViewModel(LocalStore store, IDialogService dialogs, StreamOverlay stream)
    {
        _store = store;
        _dialogs = dialogs;
        _stream = stream;

        BuildKinds();
        BuildModes();

        AddGoalCommand = new RelayCommand(_ => AddGoal());
        AddBookingCommand = new RelayCommand(_ => AddBooking());
        ChooseKindCommand = new RelayCommand(p => { if (p is RunChip c) Kind = c.Key; });
        ChooseModeCommand = new RelayCommand(p => { if (p is RunChip c) OnReached = c.Key; });
        SetCurrentWonCommand = new RelayCommand(_ => SetCurrentWon());
        EditBookingCommand = new RelayCommand(p => EditBooking(p as GoalBookingViewModel));
        FinishCommand = new RelayCommand(_ => Finish());
        DeleteBookingCommand = new AsyncRelayCommand(p => DeleteBookingAsync(p as GoalBookingViewModel));
        DeleteGoalCommand = new AsyncRelayCommand(p => DeleteGoalAsync(p as GoalRowViewModel));
        EditGoalCommand = new RelayCommand(p => StartEditGoal(p as GoalRowViewModel));
        MoveUpCommand = new RelayCommand(p => Move(p as GoalRowViewModel, -1));
        MoveDownCommand = new RelayCommand(p => Move(p as GoalRowViewModel, +1));

        MigrateCurrentWon();
        Reload();
    }

    private GoalsData Data => _store.Goals;

    /* ---------- Das aktive Ziel ---------- */

    /// Das erste offene Ziel der Warteschlange.
    private GoalDto? Active => Data.Goals.Where(g => !g.Done).OrderBy(g => g.Sort).FirstOrDefault();

    public bool HasActive => Active is not null;
    public bool HasNoGoals => Data.Goals.Count == 0;

    public string Title => Active?.Title ?? "—";
    public string TargetLabel => Active is null ? "—" : Won(EffectiveTarget(Active));

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

    /// Ausgaben: alles, was abgeht - also nicht Einnahme, nicht Bezahlt und
    /// nicht der (vorzeichenbehaftete) Abgleich.
    private decimal Expenses(GoalDto goal) =>
        goal.Bookings
            .Where(b => b.Kind != GoalBooking.Income
                     && b.Kind != GoalBooking.Paid
                     && b.Kind != GoalBooking.Adjust)
            .Sum(b => b.Amount);

    /// Was direkt aufs Ziel bezahlt wurde - senkt den **Zielbetrag selbst**
    /// (2700w, 500w bezahlt -> 2200w Ziel).
    private decimal Paid(GoalDto goal) =>
        goal.Bookings.Where(b => b.Kind == GoalBooking.Paid).Sum(b => b.Amount);

    /// Das um bereits Bezahltes verringerte Ziel. Nie unter null.
    private decimal EffectiveTarget(GoalDto goal) => Math.Max(0m, goal.Target - Paid(goal));

    /// Reine Zusatzeinnahmen.
    private decimal Income(GoalDto goal) =>
        goal.Bookings.Where(b => b.Kind == GoalBooking.Income).Sum(b => b.Amount);

    /// Der Won-Abgleich mit dem Spiel - die Summe der (vorzeichenbehafteten)
    /// Abgleich-Buchungen.
    private decimal Adjust(GoalDto goal) =>
        goal.Bookings.Where(b => b.Kind == GoalBooking.Adjust).Sum(b => b.Amount);

    /// Netto des Ziels: Laeufe + Einnahmen + Uebertrag − Ausgaben + Abgleich.
    private decimal Net(GoalDto goal) =>
        GoalCalc.Net(RunProfit(goal), Income(goal), Expenses(goal), goal.CarryIn, Adjust(goal));

    public string ProfitLabel => Active is null ? "—" : Won(RunProfit(Active));
    public string ExpensesLabel => Active is null ? "—" : Won(Expenses(Active));
    public string IncomeLabel => Active is null ? "—" : Won(Income(Active));
    public string PaidLabel => Active is null ? "—" : Won(Paid(Active));
    public bool HasPaid => Active is not null && Paid(Active) > 0m;

    /// Fortschritt des Bezahlens am **urspruenglichen** Zielbetrag, z. B.
    /// „2000w / 2700w" - der reduzierte Restbetrag allein verbirgt, wie teuer
    /// das Ziel urspruenglich war.
    public string PaidProgressLabel =>
        Active is null ? "—" : $"{Won(Paid(Active))} / {Won(Active.Target)}";
    public string CarryLabel => Active is null ? "—" : Won(Active.CarryIn);
    public bool HasCarry => Active is { CarryIn: > 0m };

    public string AdjustLabel => Active is null ? "—" : Won(Adjust(Active));
    public bool HasAdjust => Active is not null && Adjust(Active) != 0m;

    public string NetLabel => Active is null ? "—" : Won(Net(Active));
    public string RestLabel => Active is null ? "—" : Won(GoalCalc.Rest(EffectiveTarget(Active), Net(Active)));

    public double Progress => Active is null ? 0 : GoalCalc.Progress(EffectiveTarget(Active), Net(Active));
    public string ProgressLabel => Progress.ToString("0.#", CultureInfo.CurrentCulture) + " %";

    /// Wie viele Laeufe noch fehlen - geschaetzt aus dem, was der zuletzt
    /// genutzte Lauf bisher im Mittel einbrachte.
    public string RunsLeftLabel
    {
        get
        {
            if (Active is null) return "—";

            var rest = GoalCalc.Rest(EffectiveTarget(Active), Net(Active));
            if (rest <= 0m) return Loc.T("goals.reached");

            var perRun = PerRun(LastUsedRun());
            if (perRun <= 0m) return "—";

            return Loc.T("goals.runsLeft", GoalCalc.Runs(rest, perRun).ToString("N0", CultureInfo.CurrentCulture));
        }
    }

    /// Die Laufart des juengsten Run-Tracker-Eintrags, sonst der erste Lauf
    /// des Katalogs. Gleiche Wahl wie in der Stream-Einblendung.
    private string LastUsedRun() =>
        _store.Runs.Entries.OrderByDescending(e => e.AddedAt).FirstOrDefault()?.Run
        ?? RunCatalog.Runs[0].Key;

    /// Was ein Lauf dieser Art im Mittel einbringt, in Won.
    private decimal PerRun(string runKey)
    {
        var mine = _store.Runs.Entries.Where(e => e.Run == runKey).ToList();
        if (mine.Count == 0) return 0m;

        return mine.Sum(e => e.Chests * Price(e)) / mine.Count / Money.KkPerW;
    }

    /* ---------- Ein Ziel anlegen ---------- */

    public string NewTitle { get => _newTitle; set => Set(ref _newTitle, value); }
    public string NewTarget { get => _newTarget; set => Set(ref _newTarget, value); }

    public RelayCommand AddGoalCommand { get; }
    public RelayCommand EditGoalCommand { get; }

    /// Das Ziel, das gerade in der Maske bearbeitet wird; null heisst „neu".
    private int? _goalEditId;

    /// Beschriftung des Knopfs: beim Bearbeiten „Speichern", sonst „Anlegen".
    public string AddGoalLabel =>
        _goalEditId is null ? Loc.T("goals.new.add") : Loc.T("goals.edit.save");

    private void AddGoal()
    {
        if (!Read(_newTarget, out var target) || target <= 0m) return;

        // Ein bestehendes Ziel bearbeiten: Titel und Zielbetrag ueberschreiben.
        // Noetig, weil der aus alten Fassungen uebernommene Zielbetrag nicht
        // immer der eigentliche ist.
        if (_goalEditId is { } id)
        {
            var goal = Data.Goals.FirstOrDefault(g => g.Id == id);
            if (goal is not null)
            {
                goal.Title = _newTitle.Trim();
                goal.Target = target;
            }
            _goalEditId = null;
            Raise(nameof(AddGoalLabel));
        }
        else
        {
            Data.Goals.Add(new GoalDto
            {
                Id = Data.TakeId(),
                Title = _newTitle.Trim(),
                Target = target,
                Sort = Data.Goals.Count == 0 ? 0 : Data.Goals.Max(g => g.Sort) + 1,
                StartedAt = DateTime.Now,
                // Die Regel explizit setzen, damit die einmalige Migration sie nicht
                // nachtraeglich ueberschreibt.
                OnReached = Data.OnReached is { Length: > 0 } ? Data.OnReached : GoalMode.Carry,
            });
        }

        NewTitle = "";
        NewTarget = "";
        Save();
    }

    /// Lae­dt ein Ziel in die Maske, der naechste „Speichern"-Klick ueberschreibt es.
    private void StartEditGoal(GoalRowViewModel? row)
    {
        if (row is null) return;

        NewTitle = row.RawTitle;
        NewTarget = row.Target.ToString(CultureInfo.InvariantCulture);
        _goalEditId = row.Id;
        Raise(nameof(AddGoalLabel));
    }

    /* ---------- Ausgaben, Einnahmen, Abgleich ---------- */

    /// Die drei Arten als Knopfreihe.
    public ObservableCollection<RunChip> Kinds { get; } = new();

    public string Kind
    {
        get => _kind;
        set
        {
            if (!Set(ref _kind, value)) return;

            foreach (var c in Kinds) c.IsActive = c.Key == _kind;
        }
    }

    public string Amount { get => _amount; set => Set(ref _amount, value); }
    public string Note { get => _note; set => Set(ref _note, value); }

    public RelayCommand ChooseKindCommand { get; }
    public RelayCommand AddBookingCommand { get; }
    public RelayCommand EditBookingCommand { get; }
    public AsyncRelayCommand DeleteBookingCommand { get; }

    public ObservableCollection<GoalBookingViewModel> Bookings { get; } = new();

    private void AddBooking()
    {
        if (Active is null || !Read(_amount, out var amount) || amount <= 0m) return;

        if (_editId is int id)
        {
            var existing = Active.Bookings.FirstOrDefault(b => b.Id == id);
            if (existing is not null)
            {
                existing.Kind = _kind;
                existing.Amount = amount;
                existing.Note = _note.Trim();
            }

            _editId = null;
        }
        else
        {
            Active.Bookings.Add(new GoalBookingDto
            {
                Id = Data.TakeId(),
                Kind = _kind,
                Amount = amount,
                Note = _note.Trim(),
                AddedAt = DateTime.Now,
            });
        }

        Amount = "";
        Note = "";
        Save();
    }

    /// Betrag/Notiz/Art der Buchung in die Eingabefelder laden; der naechste
    /// „Eintragen"-Klick ueberschreibt dann diese Buchung.
    private void EditBooking(GoalBookingViewModel? row)
    {
        if (row is null || Active is null) return;

        var b = Active.Bookings.FirstOrDefault(x => x.Id == row.Id);
        if (b is null) return;
        if (b.Kind is not (GoalBooking.Income or GoalBooking.Expense or GoalBooking.Paid)) return;

        _editId = b.Id;
        _editGoalId = Active.Id;
        Kind = b.Kind;
        Amount = b.Amount.ToString(CultureInfo.InvariantCulture);
        Note = b.Note;
    }

    private async Task DeleteBookingAsync(GoalBookingViewModel? row)
    {
        if (row is null || Active is null) return;

        var ok = await _dialogs.ConfirmAsync(
            Loc.T("goals.booking.delete"),
            Loc.T("goals.booking.deleteAsk", row.AmountLabel, row.KindLabel));
        if (!ok) return;

        if (_editId == row.Id) _editId = null;
        Active.Bookings.RemoveAll(b => b.Id == row.Id);
        Save();
    }

    /* ---------- Won-Abgleich mit dem Spiel ---------- */

    /// „Ich habe jetzt … Won": der eingetippte Stand des aktiven Ziels.
    public string SetCurrentWonText { get => _setCurrentWon; set => Set(ref _setCurrentWon, value); }

    public RelayCommand SetCurrentWonCommand { get; }

    /// Gleicht das berechnete Netto an den eingetippten Won-Stand an, indem die
    /// Differenz als Abgleich-Buchung angehaengt wird (darf negativ sein).
    private void SetCurrentWon()
    {
        if (Active is null || !Read(_setCurrentWon, out var value)) return;

        var delta = value - Net(Active);
        if (delta != 0m)
        {
            Active.Bookings.Add(new GoalBookingDto
            {
                Id = Data.TakeId(),
                Kind = GoalBooking.Adjust,
                Amount = delta,
                Note = Loc.T("goals.adjust.note"),
                AddedAt = DateTime.Now,
            });
        }

        SetCurrentWonText = "";
        CancelEdit();
        Save();
    }

    /// Einmalige Umstellung des alten globalen `UseCurrentWon`-Overrides: der
    /// Unterschied zum berechneten Netto (ohne Override, da entfallen) wird dem
    /// aktiven Ziel als Abgleich-Buchung angehaengt. Danach wird die Fahne
    /// geloescht, sodass es genau einmal laeuft.
    private void MigrateCurrentWon()
    {
        if (!Data.UseCurrentWon) return;

        var goal = Data.Goals.Where(g => !g.Done).OrderBy(g => g.Sort).FirstOrDefault();
        if (goal is not null)
        {
            var delta = Data.CurrentWon - Net(goal);
            if (delta != 0m)
                goal.Bookings.Add(new GoalBookingDto
                {
                    Id = Data.TakeId(),
                    Kind = GoalBooking.Adjust,
                    Amount = delta,
                    Note = Loc.T("goals.adjust.note"),
                    AddedAt = DateTime.Now,
                });
        }

        Data.UseCurrentWon = false;
        _store.SaveGoals();
        _stream.Write();
    }

    /// Verwirft eine angefangene Bearbeitung, damit „Eintragen" nichts ueberschreibt.
    private void CancelEdit()
    {
        if (_editId is null) return;
        _editId = null;
        Amount = "";
        Note = "";
    }

    /* ---------- Die Warteschlange ---------- */

    public ObservableCollection<GoalRowViewModel> Queue { get; } = new();

    /// Abgeschlossene Ziele, juengstes zuerst.
    public ObservableCollection<GoalRowViewModel> DoneGoals { get; } = new();

    public bool HasDone => DoneGoals.Count > 0;

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
        // Falls genau dieses Ziel in der Maske lag, die Bearbeitung abbrechen.
        if (_goalEditId == row.Id)
        {
            _goalEditId = null;
            NewTitle = "";
            NewTarget = "";
            Raise(nameof(AddGoalLabel));
        }
        Save();
    }

    /* ---------- Was beim Erreichen geschieht ---------- */

    public ObservableCollection<RunChip> Modes { get; } = new();

    /// Leeres `OnReached` gilt als „carry".
    private static string Mode(GoalDto? goal) =>
        goal is { OnReached.Length: > 0 } ? goal.OnReached : GoalMode.Carry;

    public string OnReached
    {
        get => Mode(Active);
        set
        {
            if (Active is null || Active.OnReached == value) return;

            Active.OnReached = value;
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
        var surplus = GoalCalc.Surplus(EffectiveTarget(goal), Net(goal));

        goal.Done = true;
        goal.DoneAt = at;

        var next = Data.Goals.Where(g => !g.Done).OrderBy(g => g.Sort).FirstOrDefault();
        if (next is null) return;

        // Ab hier zaehlen die Laeufe fuer das naechste Ziel - sonst zaehlte
        // der Abend doppelt.
        next.StartedAt = at;
        next.CarryIn = Mode(goal) == GoalMode.Carry ? surplus : 0m;
    }

    /// Prueft nach jeder Aenderung, ob das aktive Ziel erreicht ist. Bei
    /// „stehen bleiben" passiert nichts - dort schaltet der Nutzer selbst.
    private void Settle()
    {
        // Mehrere auf einmal sind moeglich: wer ein kleines Ziel ueberrennt,
        // soll nicht erst beim naechsten Eintrag weiterruecken.
        for (var guard = 0; guard < Data.Goals.Count; guard++)
        {
            var goal = Active;
            if (goal is null || Mode(goal) == GoalMode.Manual) return;
            if (Net(goal) < EffectiveTarget(goal)) return;

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

        // Zielwechsel: angefangene Bearbeitung gehoert zum alten Ziel.
        if (_editId is not null && Active?.Id != _editGoalId) CancelEdit();

        Queue.Clear();
        foreach (var goal in Data.Goals.Where(g => !g.Done).OrderBy(g => g.Sort))
            Queue.Add(new GoalRowViewModel(goal, Won(EffectiveTarget(goal)), goal.Id == Active?.Id));

        DoneGoals.Clear();
        foreach (var goal in Data.Goals.Where(g => g.Done).OrderByDescending(g => g.DoneAt))
            DoneGoals.Add(new GoalRowViewModel(goal, Won(EffectiveTarget(goal)), false));

        Bookings.Clear();
        if (Active is not null)
            foreach (var b in Active.Bookings.OrderByDescending(b => b.AddedAt))
                Bookings.Add(new GoalBookingViewModel(b, Won(b.Amount)));

        foreach (var c in Kinds) c.IsActive = c.Key == _kind;
        foreach (var c in Modes) c.IsActive = c.Key == Mode(Active);

        Raise(nameof(HasActive));
        Raise(nameof(HasNoGoals));
        Raise(nameof(CanFinish));
        Raise(nameof(Title));
        Raise(nameof(TargetLabel));
        Raise(nameof(ProfitLabel));
        Raise(nameof(ExpensesLabel));
        Raise(nameof(IncomeLabel));
        Raise(nameof(PaidLabel));
        Raise(nameof(HasPaid));
        Raise(nameof(PaidProgressLabel));
        Raise(nameof(CarryLabel));
        Raise(nameof(HasCarry));
        Raise(nameof(AdjustLabel));
        Raise(nameof(HasAdjust));
        Raise(nameof(NetLabel));
        Raise(nameof(RestLabel));
        Raise(nameof(Progress));
        Raise(nameof(ProgressLabel));
        Raise(nameof(RunsLeftLabel));
        Raise(nameof(OnReached));
        Raise(nameof(HasBookings));
        Raise(nameof(HasDone));
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
        Kinds.Add(new RunChip(GoalBooking.Income, Loc.T("goals.kind.income")));
        Kinds.Add(new RunChip(GoalBooking.Expense, Loc.T("goals.kind.expense")));
        Kinds.Add(new RunChip(GoalBooking.Paid, Loc.T("goals.kind.paid")));
        foreach (var c in Kinds) c.IsActive = c.Key == _kind;
    }

    private void BuildModes()
    {
        Modes.Clear();
        Modes.Add(new RunChip(GoalMode.Carry, Loc.T("goals.mode.carry")));
        Modes.Add(new RunChip(GoalMode.Reset, Loc.T("goals.mode.reset")));
        Modes.Add(new RunChip(GoalMode.Manual, Loc.T("goals.mode.manual")));
        foreach (var c in Modes) c.IsActive = c.Key == Mode(Active);
    }

    /* ---------- Zahlen ---------- */

    internal static string Won(decimal value) =>
        value.ToString("#,0.##", CultureInfo.CurrentCulture) + " " + Loc.T("goals.won");

    /// Wie beim Truhenpreis: Punkt wie Komma.
    private static bool Read(string? text, out decimal value) =>
        decimal.TryParse((text ?? "").Trim().Replace(',', '.'),
            NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0m;
}

/// Die Arten eines Postens.
public static class GoalBooking
{
    /// Verbleibt fuer Migration/Abgleich alter Dateien.
    public const string Push = "push";
    public const string Extra = "extra";

    public const string Expense = "expense";
    public const string Income = "income";

    /// Direkt aufs Ziel bezahlt/eingezahlt - senkt „Noch offen".
    public const string Paid = "paid";

    /// Was im Shop liegt und noch nicht verkauft ist (nur Altbestand).
    public const string Shop = "shop";

    /// Won-Abgleich mit dem Spiel; der Betrag darf negativ sein.
    public const string Adjust = "adjust";

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
public sealed class GoalBookingViewModel(GoalBookingDto dto, string amount)
{
    public int Id { get; } = dto.Id;
    public string KindLabel { get; } = GoalBooking.Label(dto.Kind);
    public string AmountLabel { get; } = amount;
    public string Note { get; } = dto.Note;
    public bool HasNote { get; } = dto.Note.Length > 0;
    public string DayLabel { get; } = dto.AddedAt.ToString("dd.MM.");

    /// Nur Einnahme, Ausgabe und Bezahltes sind bearbeitbar; Abgleich und
    /// Altarten haben keine Chip-Entsprechung in der Eingabe.
    public bool CanEdit { get; } =
        dto.Kind is GoalBooking.Income or GoalBooking.Expense or GoalBooking.Paid;

    /// Einnahmen und Bezahltes stehen gruen da, Ausgaben in Bernstein - man
    /// sieht auf einen Blick, was dazukam und was abging.
    public bool IsIncome { get; } =
        dto.Kind is GoalBooking.Income or GoalBooking.Paid
        || (dto.Kind == GoalBooking.Adjust && dto.Amount > 0m);
}

/// Eine Zeile der Warteschlange.
public sealed class GoalRowViewModel(GoalDto dto, string target, bool isActive)
{
    public int Id { get; } = dto.Id;
    public string Title { get; } = dto.Title.Length > 0 ? dto.Title : target;
    public string TargetLabel { get; } = target;
    public bool IsActive { get; } = isActive;
    public bool IsDone { get; } = dto.Done;

    /// Der rohe Titel (ohne Ersatztext) und der urspruengliche Zielbetrag -
    /// fuers Zurueckladen in die Bearbeiten-Maske.
    public string RawTitle { get; } = dto.Title;
    public decimal Target { get; } = dto.Target;
}
