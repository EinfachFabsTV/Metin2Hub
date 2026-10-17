using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Der Bereich „Loot-Filter" haelt zwei Ansichten: **Nachschlagen** (Item ->
/// Kategorie) und das **Filter-Fenster** (interaktiver Editor). Die Knopfreihe
/// oben waehlt, wie bei den anderen Wirten. Der Wirt selbst haelt keine Daten.
public sealed class LootFilterHostViewModel : ViewModelBase
{
    private RunChip _chosen;

    public LootFilterHostViewModel(LocalStore store)
    {
        Editor = new LootFilterEditorViewModel(store);
        // Aus dem Nachschlagen laesst sich der Filter setzen; danach muss der
        // Editor seine Haken neu einlesen.
        Lookup = new LootFilterLookupViewModel(store, Editor.Rebuild);

        Pages.Add(new RunChip("lookup", Loc.T("lootfilter.tabLookup")));
        Pages.Add(new RunChip("editor", Loc.T("lootfilter.tabEditor")));
        _chosen = Pages[0];
        _chosen.IsActive = true;

        ChooseCommand = new RelayCommand(p => { if (p is RunChip chip) Chosen = chip; });
    }

    public LootFilterLookupViewModel Lookup { get; }
    public LootFilterEditorViewModel Editor { get; }

    public ObservableCollection<RunChip> Pages { get; } = new();

    public RunChip Chosen
    {
        get => _chosen;
        set
        {
            if (value is null || !value.Enabled || !Set(ref _chosen, value)) return;
            foreach (var chip in Pages) chip.IsActive = chip.Key == _chosen.Key;
            Raise(nameof(Current));
        }
    }

    public ViewModelBase Current => _chosen.Key == "editor" ? Editor : Lookup;

    public RelayCommand ChooseCommand { get; }

    public void RelabelAfterLanguageChange()
    {
        Pages[0].Label = Loc.T("lootfilter.tabLookup");
        Pages[1].Label = Loc.T("lootfilter.tabEditor");
        Lookup.RelabelAfterLanguageChange();
        Editor.RelabelAfterLanguageChange();
    }
}
