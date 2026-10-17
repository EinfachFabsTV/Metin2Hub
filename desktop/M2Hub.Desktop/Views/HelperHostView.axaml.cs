using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace M2Hub.Desktop.Views;

public partial class HelperHostView : UserControl
{
    public HelperHostView() => AvaloniaXamlLoader.Load(this);
}
