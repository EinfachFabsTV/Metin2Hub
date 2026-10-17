using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.Services.LootFilter;

namespace M2Hub.Desktop.ViewModels;

/// Filter-Fenster: Nachbildung des Beuteoptionen-Fensters. Je Oberkategorie ein
/// Block mit AN/AUS und den passenden Bedingungen. **Standard ist alles an**;
/// Aenderungen bleiben beim Schliessen gemerkt (`SettingsData.LootFilter`).
public sealed class LootFilterEditorViewModel : ViewModelBase
{
    /// Klassen wie im Spiel.
    private static readonly string[] Classes = ["Krieger", "Sura", "Ninja", "Schamane", "Lykaner"];
    private static readonly string[] ClassesBook = ["Krieger", "Sura", "Ninja", "Schamane", "Lykaner", "Sonstiges"];

    private readonly LocalStore _store;

    public LootFilterEditorViewModel(LocalStore store)
    {
        _store = store;
        ResetCommand = new RelayCommand(_ => Reset());
        Build();
    }

    public ObservableCollection<LootBlockViewModel> Blocks { get; } = new();

    public bool IsEmpty => Blocks.Count == 0;

    public RelayCommand ResetCommand { get; }

    /// Neu aufbauen - etwa nachdem das Nachschlagen den Filter geaendert hat.
    public void Rebuild() => Build();

    private void Build()
    {
        Blocks.Clear();
        var data = _store.Settings.LootFilter;

        foreach (var cat in LootFilterCatalog.Categories)
        {
            if (!data.Categories.TryGetValue(cat.Key, out var state))
            {
                state = new LootCatState();
                data.Categories[cat.Key] = state;
            }
            Blocks.Add(new LootBlockViewModel(cat, state, KindOf(cat.Key), ClassesFor(cat.Key), Save));
        }
        Raise(nameof(IsEmpty));
    }

    private void Save() => _store.SaveSettings();

    private void Reset()
    {
        _store.Settings.LootFilter.Categories.Clear();
        _store.SaveSettings();
        Build();
    }

    private static string[] ClassesFor(string key) => key == "fertigkeitsbuch" ? ClassesBook : Classes;

    /// Welche Steuerung eine Oberkategorie zeigt - abgeleitet wie im
    /// Beuteoptionen-Fenster.
    private static BlockKind KindOf(string key) => key switch
    {
        "waffen" or "ruestungen" or "helm" => BlockKind.Gear,
        "sonstige-ausruestung" => BlockKind.GearSubs,
        "fertigkeitsbuch" => BlockKind.ClassOnly,
        "eventitem" => BlockKind.OnOff,
        _ => BlockKind.Subs,
    };

    public void RelabelAfterLanguageChange() { /* Kategorie-Namen sind deutsch aus den Ordnern. */ }
}

public enum BlockKind { Gear, GearSubs, ClassOnly, Subs, OnOff }

/// Ein Block (Oberkategorie) im Editor.
public sealed class LootBlockViewModel : ViewModelBase
{
    private readonly LootCatState _state;
    private readonly Action _save;
    private bool _on;

    public LootBlockViewModel(LootCategory cat, LootCatState state, BlockKind kind, string[] classes, Action save)
    {
        _state = state;
        _save = save;
        Name = cat.Name;
        Kind = kind;
        _on = state.On;

        if (ShowClasses)
            foreach (var c in classes)
                Classes.Add(new ToggleItem(c, Get(state.Classes, c), v => { state.Classes[c] = v; save(); }));

        if (ShowSubs)
            foreach (var sub in cat.Subs)
                Subs.Add(new ToggleItem(sub.Name, Get(state.Subs, sub.Name), v => { state.Subs[sub.Name] = v; save(); }));
    }

    public string Name { get; }
    public BlockKind Kind { get; }

    public bool On
    {
        get => _on;
        set { if (!Set(ref _on, value)) return; _state.On = value; Raise(nameof(OnLabel)); _save(); }
    }

    public string OnLabel => _on ? "AN" : "AUS";

    public ObservableCollection<ToggleItem> Classes { get; } = new();
    public ObservableCollection<ToggleItem> Subs { get; } = new();

    public bool ShowClasses => Kind is BlockKind.Gear or BlockKind.ClassOnly;
    public bool ShowSubs => Kind is BlockKind.Subs or BlockKind.GearSubs;
    public bool ShowRanges => Kind is BlockKind.Gear or BlockKind.GearSubs;

    // Beim Setzen schreiben wir in den Zustand und speichern.
    public int RefineMin { get => _state.RefineMin; set { _state.RefineMin = value; Raise(nameof(RefineMin)); _save(); } }
    public int RefineMax { get => _state.RefineMax; set { _state.RefineMax = value; Raise(nameof(RefineMax)); _save(); } }
    public int LevelMin { get => _state.LevelMin; set { _state.LevelMin = value; Raise(nameof(LevelMin)); _save(); } }
    public int LevelMax { get => _state.LevelMax; set { _state.LevelMax = value; Raise(nameof(LevelMax)); _save(); } }

    private static bool Get(Dictionary<string, bool> map, string key) =>
        !map.TryGetValue(key, out var v) || v; // fehlt = an
}

/// Ein Haken mit Namen; speichert beim Umschalten.
public sealed class ToggleItem(string name, bool @checked, Action<bool> onChange) : ViewModelBase
{
    private bool _checked = @checked;

    public string Name { get; } = name;

    public bool Checked
    {
        get => _checked;
        set { if (Set(ref _checked, value)) onChange(value); }
    }
}
