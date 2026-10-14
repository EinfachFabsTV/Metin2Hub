using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Der Bereich „Rechner" haelt mehrere Rechner; die Knopfreihe oben waehlt.
/// Der Wirt rechnet nichts - er reicht nur durch, wie der Goals-Wirt.
///
/// Aktuell: Gilde, Pet, Biologen. Weitere (Equipment) folgen nach demselben
/// Muster - je ein ViewModel, hier als Chip eingehaengt.
public sealed class CalcHostViewModel : ViewModelBase
{
    private RunChip _chosen;

    public CalcHostViewModel()
    {
        Guild = new GuildCalcViewModel();
        Pet = new PetCalcViewModel();
        Bio = new BioCalcViewModel();

        Pages.Add(new RunChip("gilde", Loc.T("calc.nav.gilde")));
        Pages.Add(new RunChip("pet", Loc.T("calc.nav.pet")));
        Pages.Add(new RunChip("biologen", Loc.T("calc.nav.biologen")));
        _chosen = Pages[0];
        _chosen.IsActive = true;

        ChooseCommand = new RelayCommand(p => { if (p is RunChip chip) Chosen = chip; });
    }

    public GuildCalcViewModel Guild { get; }
    public PetCalcViewModel Pet { get; }
    public BioCalcViewModel Bio { get; }

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

    public ViewModelBase Current => _chosen.Key switch
    {
        "pet" => Pet,
        "biologen" => Bio,
        _ => Guild,
    };

    public RelayCommand ChooseCommand { get; }

    public void RelabelAfterLanguageChange()
    {
        Guild.RelabelAfterLanguageChange();
        Pet.RelabelAfterLanguageChange();
        Bio.RelabelAfterLanguageChange();

        Pages[0].Label = Loc.T("calc.nav.gilde");
        Pages[1].Label = Loc.T("calc.nav.pet");
        Pages[2].Label = Loc.T("calc.nav.biologen");
    }
}
