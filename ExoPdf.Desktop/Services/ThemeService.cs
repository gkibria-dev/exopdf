using System.Windows;
using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

/// <summary>
/// The only class that touches <see cref="Application.ThemeMode"/>, which is an
/// experimental API (WPF0001, suppressed in the project file). See ADR-005.
/// </summary>
public class ThemeService : IThemeService
{
    public void Apply(AppTheme theme) =>
        Application.Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };
}
