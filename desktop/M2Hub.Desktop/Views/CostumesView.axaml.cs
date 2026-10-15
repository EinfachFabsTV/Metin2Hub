using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace M2Hub.Desktop.Views;

public partial class CostumesView : UserControl
{
    public CostumesView() => AvaloniaXamlLoader.Load(this);

    /// Float-Button nur zeigen, wenn nennenswert gescrollt wurde.
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer sv && this.FindControl<Button>("ToTop") is { } btn)
            btn.IsVisible = sv.Offset.Y > 300;
    }

    private void OnToTop(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<ScrollViewer>("Scroller") is { } sv)
            sv.Offset = sv.Offset.WithY(0);
    }
}
