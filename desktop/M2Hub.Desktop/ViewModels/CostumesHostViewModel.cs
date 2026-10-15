using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Der Bereich „Kostüme" haelt zwei Seiten: den **Katalog** und den
/// **Simulator**. Die Knopfreihe oben waehlt, wie beim Goals-Wirt. Der Wirt
/// selbst haelt keine Daten.
public sealed class CostumesHostViewModel : ViewModelBase
{
    private RunChip _chosen;

    public CostumesHostViewModel()
    {
        Catalog = new CostumesViewModel();
        Simulator = new SimulatorViewModel();

        Pages.Add(new RunChip("katalog", Loc.T("costumes.tabCatalog")));
        Pages.Add(new RunChip("simulator", Loc.T("costumes.tabSimulator")));
        _chosen = Pages[0];
        _chosen.IsActive = true;

        ChooseCommand = new RelayCommand(p => { if (p is RunChip chip) Chosen = chip; });
    }

    public CostumesViewModel Catalog { get; }
    public SimulatorViewModel Simulator { get; }

    public ObservableCollection<RunChip> Pages { get; } = new();

    public RunChip Chosen
    {
        get => _chosen;
        set
        {
            if (value is null || !Set(ref _chosen, value)) return;
            foreach (var chip in Pages) chip.IsActive = chip.Key == _chosen.Key;
            Raise(nameof(Current));
        }
    }

    public ViewModelBase Current => _chosen.Key == "simulator" ? Simulator : Catalog;

    public RelayCommand ChooseCommand { get; }

    public void RelabelAfterLanguageChange()
    {
        Catalog.RelabelAfterLanguageChange();
        Simulator.RelabelAfterLanguageChange();
        Pages[0].Label = Loc.T("costumes.tabCatalog");
        Pages[1].Label = Loc.T("costumes.tabSimulator");
    }
}
