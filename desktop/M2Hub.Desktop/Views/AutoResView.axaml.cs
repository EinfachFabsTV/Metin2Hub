using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using M2Hub.Desktop.ViewModels;

namespace M2Hub.Desktop.Views;

public partial class AutoResView : UserControl
{
    public AutoResView() => AvaloniaXamlLoader.Load(this);

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AutoResViewModel vm) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "metin2.cfg",
            AllowMultiple = false,
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path)) vm.ConfigPath = path;
    }
}
