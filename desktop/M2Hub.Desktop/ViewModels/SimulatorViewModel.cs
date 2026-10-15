using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media.Imaging;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.Costumes;
using static M2Hub.Desktop.Services.Costumes.SetSimulator;

namespace M2Hub.Desktop.ViewModels;

/// Set-Simulator als Kachel-Auswahl: je Slot (Ring, Body, Frisur, Waffe, Pet,
/// Reittier) ein Teil anklicken, rechts die Gesamt-EXP und die aktiven Boni.
/// „Beste EXP" sucht die stärkste Kombination – zum gewählten Ring, wenn einer
/// gewählt ist. Rechenweg in Services/Costumes/SetSimulator.
public sealed class SimulatorViewModel : ViewModelBase
{
    private bool _male = true;
    private string _query = "";

    public SimulatorViewModel()
    {
        BestCommand = new RelayCommand(_ => ApplyBest());
        ResetCommand = new RelayCommand(_ => Reset());
        GenderCommand = new RelayCommand(p => Male = (p as string) != "w");

        foreach (var slot in Slots)
            SlotGrids.Add(new SimSlotGrid(slot, Loc.T(SlotKey(slot)), Recompute));

        Refilter();
        Recompute();
    }

    public ObservableCollection<SimSlotGrid> SlotGrids { get; } = new();

    public RelayCommand BestCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand GenderCommand { get; }

    /// Geschlechtsfilter fuer Body/Frisur. Standard maennlich.
    public bool Male
    {
        get => _male;
        set { if (Set(ref _male, value)) { Raise(nameof(Female)); Refilter(); } }
    }

    public bool Female => !_male;

    public string Query
    {
        get => _query;
        set { if (Set(ref _query, value ?? "")) Refilter(); }
    }

    public string TotalLabel { get; private set; } = "";
    public ObservableCollection<BonusRow> OwnBonuses { get; } = new();
    public ObservableCollection<BonusRow> SetBonuses { get; } = new();
    public bool HasOwn { get; private set; }
    public bool HasSets { get; private set; }
    public bool HasAny { get; private set; }

    private void Refilter()
    {
        var q = _query.Trim();
        foreach (var g in SlotGrids) g.Refilter(_male, q);
    }

    private void Recompute()
    {
        var sel = SlotGrids.ToDictionary(g => g.Slot, g => g.Selected);
        var r = Evaluate(sel);

        TotalLabel = $"+{r.TotalExp.ToString("0.#", CultureInfo.CurrentCulture)} %";

        OwnBonuses.Clear();
        foreach (var b in r.Own) OwnBonuses.Add(new BonusRow(b.Label, Pct(b.Exp)));
        SetBonuses.Clear();
        foreach (var b in r.SetBonuses) SetBonuses.Add(new BonusRow(b.Label, Pct(b.Exp)));

        HasOwn = r.Own.Count > 0;
        HasSets = r.SetBonuses.Count > 0;
        HasAny = sel.Values.Any(i => i is not null);

        Raise(nameof(TotalLabel));
        Raise(nameof(HasOwn));
        Raise(nameof(HasSets));
        Raise(nameof(HasAny));
    }

    private void ApplyBest()
    {
        var ring = SlotGrids.First(g => g.Slot == Slot.Ring).Selected;
        var best = Best(ring);
        foreach (var g in SlotGrids) g.Select(best.GetValueOrDefault(g.Slot));
        Recompute();
    }

    private void Reset()
    {
        foreach (var g in SlotGrids) g.Select(null);
        Recompute();
    }

    public void RelabelAfterLanguageChange()
    {
        foreach (var g in SlotGrids) g.Relabel(Loc.T(SlotKey(g.Slot)));
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

/// Ein Slot als Kachel-Raster.
public sealed class SimSlotGrid : ViewModelBase
{
    private readonly Action _onChange;
    private readonly List<SimTile> _all = new();
    private SimItem? _selected;
    private string _title;

    public SimSlotGrid(Slot slot, string title, Action onChange)
    {
        Slot = slot;
        _title = title;
        _onChange = onChange;
        HasGender = slot is Slot.Body or Slot.Frisur;
        SelectCommand = new RelayCommand(p => { if (p is SimTile t) Toggle(t); });

        foreach (var item in SetSimulator.Pools[slot]) _all.Add(new SimTile(item));
    }

    public Slot Slot { get; }
    public string Title { get => _title; private set => Set(ref _title, value); }
    public bool HasGender { get; }
    public SimItem? Selected => _selected;

    /// Sichtbare Kacheln nach Geschlecht + Suche.
    public ObservableCollection<SimTile> Tiles { get; } = new();

    public RelayCommand SelectCommand { get; }

    public void Refilter(bool male, string query)
    {
        Tiles.Clear();
        foreach (var t in _all)
        {
            if (HasGender && t.Gender.Length > 0 && t.Gender != (male ? "m" : "w")) continue;
            if (query.Length > 0 && !t.Label.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            Tiles.Add(t);
        }
    }

    private void Toggle(SimTile tile)
    {
        if (_selected == tile.Item)
        {
            _selected = null;
            tile.IsSelected = false;
        }
        else
        {
            foreach (var t in _all) t.IsSelected = t == tile;
            _selected = tile.Item;
        }
        _onChange();
    }

    /// Von „Beste EXP"/Reset gesetzt, ohne das Raster neu zu bauen.
    public void Select(SimItem? item)
    {
        _selected = item;
        foreach (var t in _all) t.IsSelected = item is not null && t.Item == item;
    }

    public void Relabel(string title) => Title = title;
}

/// Eine Kachel im Slot-Raster.
public sealed class SimTile : ViewModelBase
{
    private bool _selected;

    public SimTile(SimItem item)
    {
        Item = item;
        Label = item.Label;
        Gender = item.Gender;
        Icon = item.File is { } f ? CostumeCatalog.Image(f) : null;
    }

    public SimItem Item { get; }
    public string Label { get; }
    public string Gender { get; }
    public Bitmap? Icon { get; }
    public bool IsSelected { get => _selected; set => Set(ref _selected, value); }
}
