using ExoPdf.Desktop.Models;
using ExoPdf.Desktop.ViewModels;

namespace ExoPdf.Tests;

public class SettingsViewModelTests
{
    private readonly FakeSettingsStore _store = new();
    private readonly FakeThemeService _theme = new();

    private SettingsViewModel CreateViewModel() => new(_store, _theme);

    [Fact]
    public void NewViewModel_ShowsSavedTheme_WithoutApplyingOrSaving()
    {
        _store.Current.Theme = AppTheme.Dark;

        var vm = CreateViewModel();

        Assert.Equal(AppTheme.Dark, vm.Theme);
        Assert.Empty(_theme.Applied);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public void ChangingTheme_AppliesItImmediatelyAndSaves()
    {
        var vm = CreateViewModel();

        vm.Theme = AppTheme.Light;

        Assert.Equal([AppTheme.Light], _theme.Applied);
        Assert.Equal(AppTheme.Light, _store.Current.Theme);
        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public void ThemeOptions_OffersSystemLightAndDark_SystemFirst()
    {
        var vm = CreateViewModel();

        Assert.Equal([AppTheme.System, AppTheme.Light, AppTheme.Dark], vm.ThemeOptions.Select(o => o.Value));
    }

    [Fact]
    public void Placement_IsFooter()
    {
        Assert.Equal(NavigationPlacement.Footer, CreateViewModel().Placement);
    }
}
