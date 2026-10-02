using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Der Bereich „Rechner" haelt mehrere Rechner. Welcher gezeigt wird, waehlt
/// eine Knopfreihe oben - dieselben Chips wie bei den Laeufen.
///
/// Der Wirt selbst rechnet nichts; er reicht nur durch.
public sealed class CalcHostViewModel : ViewModelBase
{
    private RunChip _chosen;

    public CalcHostViewModel(LocalStore store)
    {
        Guild = new GuildCalcViewModel();
        Debt = new DebtCalcViewModel(store);

        Calculators.Add(new RunChip("guild", Loc.T("calc.guild.title")));
        Calculators.Add(new RunChip("debt", Loc.T("calc.debt.title")));
        _chosen = Calculators[0];
        _chosen.IsActive = true;

        ChooseCommand = new RelayCommand(p => { if (p is RunChip chip) Chosen = chip; });
    }

    public RelayCommand ChooseCommand { get; }

    public GuildCalcViewModel Guild { get; }
    public DebtCalcViewModel Debt { get; }

    public ObservableCollection<RunChip> Calculators { get; } = new();

    public RunChip Chosen
    {
        get => _chosen;
        set
        {
            if (value is null || !Set(ref _chosen, value)) return;

            foreach (var chip in Calculators) chip.IsActive = chip.Key == _chosen.Key;
            Raise(nameof(Current));
        }
    }

    public ViewModelBase Current => _chosen.Key == "debt" ? Debt : Guild;

    /// Beim Wechsel auf den Bereich: der Schulden-Rechner liest aus runs.json,
    /// und dort kann inzwischen etwas dazugekommen sein.
    public void Reload() => Debt.Reload();

    public void RelabelAfterLanguageChange()
    {
        Guild.RelabelAfterLanguageChange();
        Debt.RelabelAfterLanguageChange();

        Calculators[0].Label = Loc.T("calc.guild.title");
        Calculators[1].Label = Loc.T("calc.debt.title");
    }
}
