using CommunityToolkit.Mvvm.ComponentModel;
using ExoPdf.Desktop.Models;
using ExoPdf.Desktop.Services;

namespace ExoPdf.Desktop.ViewModels;

public record ThemeOption(AppTheme Value, string Label);

public partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsStore _settings;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private AppTheme _theme;

    public SettingsViewModel(ISettingsStore settings, IThemeService themeService)
        : base("Settings", "\uE713", NavigationPlacement.Footer)
    {
        _settings = settings;
        _themeService = themeService;
        _theme = settings.Current.Theme;
    }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } =
    [
        new(AppTheme.System, "Use system setting"),
        new(AppTheme.Light, "Light"),
        new(AppTheme.Dark, "Dark")
    ];

    partial void OnThemeChanged(AppTheme value)
    {
        _themeService.Apply(value);
        _settings.Current.Theme = value;
        _settings.Save();
    }
}
