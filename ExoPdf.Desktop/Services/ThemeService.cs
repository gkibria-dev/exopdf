using System.Windows;
using System.Windows.Interop;
using ExoPdf.Desktop.Models;
using Microsoft.Win32;

namespace ExoPdf.Desktop.Services;

/// <summary>
/// The only class that touches theme APIs: <see cref="Application.ThemeMode"/>, which is an
/// experimental API (WPF0001, suppressed in the project file; see ADR-005), and the
/// native title bar, which ThemeMode does not darken on Windows 10.
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private AppTheme _theme = AppTheme.System;
    private bool _hooked;

    public void Apply(AppTheme theme)
    {
        _theme = theme;

        Application.Current.ThemeMode = theme switch
        {
            AppTheme.Light => ThemeMode.Light,
            AppTheme.Dark => ThemeMode.Dark,
            _ => ThemeMode.System
        };

        Hook();
        ApplyToOpenWindows();
    }

    public void Dispose()
    {
        if (_hooked)
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void Hook()
    {
        if (_hooked)
            return;

        _hooked = true;

        // A window's handle exists once it has loaded; style windows created later too.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window)
            ApplyTo(window);
    }

    // Raised on a system thread when Windows settings change, including the app-mode colour.
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (_theme == AppTheme.System)
            Application.Current?.Dispatcher.BeginInvoke(ApplyToOpenWindows);
    }

    private void ApplyToOpenWindows()
    {
        foreach (Window window in Application.Current.Windows)
            ApplyTo(window);
    }

    private void ApplyTo(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
            DwmTitleBar.SetDark(handle, TitleBarTheme.IsDark(_theme, SystemPrefersDark()));
    }

    private static bool SystemPrefersDark() =>
        Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            1) is int useLight && useLight == 0;
}
