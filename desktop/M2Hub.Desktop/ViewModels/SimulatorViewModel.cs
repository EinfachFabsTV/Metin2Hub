using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Costumes;
using static M2Hub.Desktop.Services.Costumes.SetSimulator;

namespace M2Hub.Desktop.ViewModels;

/// Set-Simulator: je Slot (Ring, Body, Frisur, Waffe, Pet, Reittier) ein Teil
/// waehlen und die Gesamt-EXP live sehen; „Beste EXP" sucht die st-aerkste
/// Kombination. Rechenweg in Services/Costumes/SetSimulator.
public sealed class SimulatorViewModel : ViewModelBase
{
    public SimulatorViewModel()
    {
        BestCommand = new RelayCommand(_ => ApplyBest());
        ResetCommand = new RelayCommand(_ => Reset());

        foreach (var slot in Slots)
            SlotPickers.Add(new SimSlot(slot, Loc.T(SlotKey(slot)), Recompute));

        Recompute();
    }

    public ObservableCollection<SimSlot> SlotPickers { get; } = new();

    public RelayCommand BestCommand { get; }
    public RelayCommand ResetCommand { get; }

    public string TotalLabel { get; private set; } = "";
    public ObservableCollection<BonusRow> OwnBonuses { get; } = new();
    public ObservableCollection<BonusRow> SetBonuses { get; } = new();
    public bool HasOwn { get; private set; }
    public bool HasSets { get; private set; }

    private void Recompute()
    {
        var sel = SlotPickers.ToDictionary(p => p.Slot, p => p.Selected?.Item);
        var r = Evaluate(sel);

        TotalLabel = $"+{r.TotalExp.ToString("0.#", CultureInfo.CurrentCulture)} %";

        OwnBonuses.Clear();
        foreach (var b in r.Own) OwnBonuses.Add(new BonusRow(b.Label, Pct(b.Exp)));
        SetBonuses.Clear();
        foreach (var b in r.SetBonuses) SetBonuses.Add(new BonusRow(b.Label, Pct(b.Exp)));

        HasOwn = r.Own.Count > 0;
        HasSets = r.SetBonuses.Count > 0;

        Raise(nameof(TotalLabel));
        Raise(nameof(HasOwn));
        Raise(nameof(HasSets));
    }

    private void ApplyBest()
    {
        var best = Best();
        foreach (var p in SlotPickers)
            p.Select(best.GetValueOrDefault(p.Slot));
        Recompute();
    }

    private void Reset()
    {
        foreach (var p in SlotPickers) p.Select(null);
        Recompute();
    }

    public void RelabelAfterLanguageChange()
    {
        foreach (var p in SlotPickers) p.Relabel(Loc.T(SlotKey(p.Slot)));
        Recompute();
    }

    private static string Pct(double exp) => $"+{exp.ToString("0.#", CultureInfo.CurrentCulture)} %";

    private static string SlotKey(Slot s) => s switch
    {
        Slot.Ring => "costumes.slot.ring",
        Slot.Body => "costumes.slot.body",
        Slot.Frisur => "costumes.slot.frisur",
        Slot.Weapon => "costumes.slot.weapon",
        Slot.Pet => "costumes.slot.pet",
        _ => "costumes.slot.mount",
    };
}

public sealed record BonusRow(string Label, string Value);

/// Eine Slot-Auswahl mit ihren Optionen.
public sealed class SimSlot : ViewModelBase
{
    private readonly Action _onChange;
    private readonly Dictionary<SimItem, SimOption> _byItem = new();
    private SimOption _selected;
    private string _title;

    public SimSlot(Slot slot, string title, Action onChange)
    {
        Slot = slot;
        _title = title;
        _onChange = onChange;

        var none = new SimOption(null, Loc.T("costumes.sim.none"));
        Options.Add(none);
        foreach (var item in SetSimulator.Pools[slot])
        {
            var opt = new SimOption(item, "");
            _byItem[item] = opt;
            Options.Add(opt);
        }
        _selected = none;
    }

    public Slot Slot { get; }
    public string Title { get => _title; private set => Set(ref _title, value); }
    public ObservableCollection<SimOption> Options { get; } = new();

    public SimOption Selected
    {
        get => _selected;
        set { if (value is not null && Set(ref _selected, value)) _onChange(); }
    }

    /// Von „Beste EXP"/Reset gesetzt, ohne mehrfach zu rechnen.
    public void Select(SimItem? item)
    {
        var opt = item is not null && _byItem.TryGetValue(item, out var o) ? o : Options[0];
        Set(ref _selected, opt, nameof(Selected));
    }

    public void Relabel(string title) => Title = title;
}

/// Eine Option in der Slot-Liste (Teil oder „keins").
public sealed class SimOption
{
    public SimOption(SimItem? item, string noneLabel)
    {
        Item = item;
        Label = item?.Label ?? noneLabel;
        Icon = item?.File is { } f ? CostumeCatalog.Image(f) : null;
        HasIcon = Icon is not null;
    }

    public SimItem? Item { get; }
    public string Label { get; }
    public Bitmap? Icon { get; }
    public bool HasIcon { get; }
}
