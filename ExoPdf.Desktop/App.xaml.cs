using System.Windows;
using System.IO.Abstractions;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Operations;
using ExoPdf.Desktop.Services;
using ExoPdf.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ExoPdf.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _services = BuildServices();

        var settings = _services.GetRequiredService<ISettingsStore>();
        _services.GetRequiredService<IThemeService>().Apply(settings.Current.Theme);

        var window = new MainWindow { DataContext = _services.GetRequiredService<MainViewModel>() };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<MergeOutputNamer>();
        services.AddSingleton<IMergeSourceFinder, MergeSourceFinder>();
        services.AddSingleton<IPdfMerger, PdfMerger>();
        services.AddSingleton<IFolderPicker, FolderPicker>();
        services.AddSingleton<IShellLauncher, ShellLauncher>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ISettingsStore>(_ => new JsonSettingsStore(JsonSettingsStore.DefaultFilePath));

        // Each page shows up in the sidebar. To add an operation: register its
        // ViewModel here and map it to its View in App.xaml.
        services.AddSingleton<PageViewModel, MergeViewModel>();
        services.AddSingleton<PageViewModel, SettingsViewModel>();

        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
