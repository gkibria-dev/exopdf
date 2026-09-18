using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

public static class TitleBarTheme
{
    /// <summary>Whether the title bar should be dark for the chosen theme.</summary>
    /// <param name="systemPrefersDark">Whether Windows is set to dark app mode; used only for <see cref="AppTheme.System"/>.</param>
    public static bool IsDark(AppTheme theme, bool systemPrefersDark) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => systemPrefersDark
    };
}
