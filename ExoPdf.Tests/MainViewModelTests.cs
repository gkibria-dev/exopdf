using ExoPdf.Desktop.ViewModels;

namespace ExoPdf.Tests;

public class MainViewModelTests
{
    private readonly TestPage _merge = new("Merge");
    private readonly TestPage _split = new("Split");
    private readonly TestPage _settings = new("Settings", NavigationPlacement.Footer);

    [Fact]
    public void Constructor_SplitsPagesByPlacement()
    {
        var vm = new MainViewModel([_merge, _split, _settings]);

        Assert.Equal([_merge, _split], vm.Pages);
        Assert.Equal([_settings], vm.FooterPages);
    }

    [Fact]
    public void Constructor_ShowsFirstPage()
    {
        var vm = new MainViewModel([_merge, _split, _settings]);

        Assert.Same(_merge, vm.CurrentPage);
        Assert.Same(_merge, vm.SelectedPage);
        Assert.Null(vm.SelectedFooterPage);
    }

    [Fact]
    public void SelectingMainPage_ChangesCurrentPage()
    {
        var vm = new MainViewModel([_merge, _split, _settings]);

        vm.SelectedPage = _split;

        Assert.Same(_split, vm.CurrentPage);
    }

    [Fact]
    public void SelectingFooterPage_ChangesCurrentPageAndClearsMainSelection()
    {
        var vm = new MainViewModel([_merge, _settings]);

        vm.SelectedFooterPage = _settings;

        Assert.Same(_settings, vm.CurrentPage);
        Assert.Null(vm.SelectedPage);
    }

    [Fact]
    public void SelectingMainPageAfterFooter_ClearsFooterSelection()
    {
        var vm = new MainViewModel([_merge, _settings]);
        vm.SelectedFooterPage = _settings;

        vm.SelectedPage = _merge;

        Assert.Same(_merge, vm.CurrentPage);
        Assert.Null(vm.SelectedFooterPage);
    }

    [Fact]
    public void DeselectingCurrentPage_RestoresSelection()
    {
        var vm = new MainViewModel([_merge, _settings]);

        vm.SelectedPage = null; // Ctrl+click on the selected entry

        Assert.Same(_merge, vm.CurrentPage);
        Assert.Same(_merge, vm.SelectedPage);
    }

    [Fact]
    public void DeselectingCurrentFooterPage_RestoresSelection()
    {
        var vm = new MainViewModel([_merge, _settings]);
        vm.SelectedFooterPage = _settings;

        vm.SelectedFooterPage = null;

        Assert.Same(_settings, vm.CurrentPage);
        Assert.Same(_settings, vm.SelectedFooterPage);
        Assert.Null(vm.SelectedPage);
    }

    [Fact]
    public void OnlyFooterPages_StillShowsAPage()
    {
        var vm = new MainViewModel([_settings]);

        Assert.Same(_settings, vm.CurrentPage);
        Assert.Same(_settings, vm.SelectedFooterPage);
    }

    [Fact]
    public void NoPages_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MainViewModel([]));
    }
}
