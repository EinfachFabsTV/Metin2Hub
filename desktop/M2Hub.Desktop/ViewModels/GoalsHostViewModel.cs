using System.Collections.ObjectModel;
using M2Hub.Desktop.Services;

namespace M2Hub.Desktop.ViewModels;

/// Der Bereich „Goals" haelt zwei Seiten derselben Frage: was man erreichen
/// will (Ziele) und was man noch schuldet (Schulden).
///
/// Beides rechnet mit denselben Laeufen und gehoert deshalb zusammen - im
/// Bereich „Rechner" stand der Schulden-Rechner neben dem Gilden-Rechner, mit
/// dem er nichts zu tun hat.
///
/// Der Wirt rechnet nichts; er reicht nur durch.
public sealed class GoalsHostViewModel : ViewModelBase
{
    private RunChip _chosen;

    public GoalsHostViewModel(LocalStore store, IDialogService dialogs, StreamOverlay stream)
    {
        Goals = new GoalsViewModel(store, dialogs, stream);
        Debt = new DebtCalcViewModel(store, stream);

        Pages.Add(new RunChip("goals", Loc.T("goals.title")));
        Pages.Add(new RunChip("debt", Loc.T("calc.debt.title")));
        _chosen = Pages[0];
        _chosen.IsActive = true;

        ChooseCommand = new RelayCommand(p => { if (p is RunChip chip) Chosen = chip; });
    }

    public GoalsViewModel Goals { get; }
    public DebtCalcViewModel Debt { get; }

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

    public ViewModelBase Current => _chosen.Key == "debt" ? Debt : Goals;

    public RelayCommand ChooseCommand { get; }

    /// Beim Wechsel auf den Bereich: beide lesen aus runs.json, und dort kann
    /// inzwischen etwas dazugekommen sein.
    public void Reload()
    {
        Goals.Reload();
        Debt.Reload();
    }

    public void RelabelAfterLanguageChange()
    {
        Goals.RelabelAfterLanguageChange();
        Debt.RelabelAfterLanguageChange();

        Pages[0].Label = Loc.T("goals.title");
        Pages[1].Label = Loc.T("calc.debt.title");
    }
}
