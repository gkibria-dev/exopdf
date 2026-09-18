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

    // async void because this is an event-style override. A failure after the await is
    // rethrown on the UI thread and reaches OnUnhandledException below.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnUnhandledException;

        _services = BuildServices();

        var settings = _services.GetRequiredService<ISettingsService>();
        _services.GetRequiredService<IThemeService>().Apply(settings.Current.Theme);

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        var window = new MainWindow { DataContext = mainViewModel };
        MainWindow = window;
        window.Show();

        // Start-up work such as listing the last folder runs after the window is visible,
        // so a slow disk or network share cannot delay it.
        await mainViewModel.InitializeAsync();
    }

    // DUI-N7: an unexpected error is reported, not swallowed and not a silent exit.
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"ExoPdf ran into an unexpected problem.{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}",
            "ExoPdf",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;

        // A failure before the window exists leaves nothing to interact with: exit
        // instead of running on with no window.
        if (MainWindow is null)
            Shutdown(1);
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
        services.AddSingleton<ISettingsStore>(sp =>
            new JsonSettingsStore(sp.GetRequiredService<IFileSystem>(), JsonSettingsStore.DefaultFilePath));
        services.AddSingleton<ISettingsService, SettingsService>();

        // Each page shows up in the sidebar. To add an operation: register its
        // ViewModel here and map it to its View in App.xaml.
        services.AddSingleton<PageViewModel, MergeViewModel>();
        services.AddSingleton<PageViewModel, SettingsViewModel>();

        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
