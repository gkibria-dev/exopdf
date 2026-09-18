using ExoPdf.Desktop.Models;
using ExoPdf.Desktop.Services;

namespace ExoPdf.Tests;

public class TitleBarThemeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dark_IsAlwaysDark(bool systemPrefersDark)
    {
        Assert.True(TitleBarTheme.IsDark(AppTheme.Dark, systemPrefersDark));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Light_IsAlwaysLight(bool systemPrefersDark)
    {
        Assert.False(TitleBarTheme.IsDark(AppTheme.Light, systemPrefersDark));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void System_FollowsWindows(bool systemPrefersDark)
    {
        Assert.Equal(systemPrefersDark, TitleBarTheme.IsDark(AppTheme.System, systemPrefersDark));
    }
}
