using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Views;

public partial class RunsView : UserControl
{
    public RunsView() => AvaloniaXamlLoader.Load(this);

    /// Enter im Truhenfeld traegt den Lauf ein. Sonst muesste man nach jeder
    /// Zahl zur Maus greifen - und ein Abend besteht aus zwanzig Laeufen.
    private void ChestsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not RunsViewModel vm) return;

        vm.AddCommand.Execute(null);
        e.Handled = true;
    }

    /// Enter im Preisfeld uebernimmt den Preis. Die Bindung greift sonst erst
    /// beim Verlassen des Feldes - wer tippt und Enter drueckt, sieht sonst
    /// nichts geschehen.
    private void PriceKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box) return;
        if (DataContext is not RunsViewModel vm) return;

        vm.ChestPriceText = box.Text ?? "";
        e.Handled = true;
    }
}
