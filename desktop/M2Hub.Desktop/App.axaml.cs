using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using M2Hub.Desktop.Services;
using M2Hub.Desktop.ViewModels;
using M2Hub.Desktop.Views;

namespace M2Hub.Desktop;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Reste eines frueheren Selbstaustauschs wegraeumen
            UpdateService.CleanupBackup();

            var store = new LocalStore();
            // Sprache vor dem ersten Fenster setzen, sonst steht kurz Deutsch da
            Loc.I.SetLanguage(store.Settings.Language);
            var forum = new ForumService(store);

            var window = new MainWindow();
            // Masken werden ueber die Ansicht gelegt statt als zweites Fenster
            var dialogs = new DialogHost();
            var vm = new MainWindowViewModel(store, forum, dialogs);

            window.DataContext = vm;
            desktop.MainWindow = window;

            // Gespeicherten Stand zeigen und den ersten Abruf starten, sobald
            // das Fenster steht.
            window.Opened += async (_, _) => await vm.InitializeAsync();
            window.Closing += (_, _) => vm.Shutdown();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
