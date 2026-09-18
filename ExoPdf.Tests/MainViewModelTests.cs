using ExoPdf.Desktop.ViewModels;

namespace ExoPdf.Tests;

public class MainViewModelTests
{
    private readonly TestPage _merge = new("Merge");
    private readonly TestPage _split = new("Split");
    private readonly TestPage _settings = new("Settings", NavigationPlacement.Footer);

    private static IEnumerable<PageViewModel> Selected(params PageViewModel[] pages) => pages.Where(p => p.IsSelected);

    [Fact]
    public void Constructor_SplitsPagesByPlacement()
    {
        var vm = new MainViewModel([_merge, _split, _settings]);

        Assert.Equal([_merge, _split], vm.Pages);
        Assert.Equal([_settings], vm.FooterPages);
    }

    [Fact]
    public void Constructor_ShowsAndSelectsTheFirstOperation()
    {
        var vm = new MainViewModel([_merge, _split, _settings]);

        Assert.Same(_merge, vm.CurrentPage);
        Assert.Equal([_merge], Selected(_merge, _split, _settings));
    }

    [Fact]
    public void Constructor_FooterListedFirst_StillShowsTheFirstOperation()
    {
        var vm = new MainViewModel([_settings, _merge]);

        Assert.Same(_merge, vm.CurrentPage);
    }

    [Fact]
    public void SelectingAnotherOperation_ChangesTheCurrentPage_AndMovesTheSelection()
    {
        var vm = new MainViewModel([_merge, _split, _settings]);

        _split.IsSelected = true;

        Assert.Same(_split, vm.CurrentPage);
        Assert.Equal([_split], Selected(_merge, _split, _settings));
    }

    [Fact]
    public void SelectingAFooterPage_ChangesTheCurrentPage_AndDeselectsTheOperation()
    {
        var vm = new MainViewModel([_merge, _settings]);

        _settings.IsSelected = true;

        Assert.Same(_settings, vm.CurrentPage);
        Assert.Equal([_settings], Selected(_merge, _settings));
    }

    [Fact]
    public void SelectingAnOperationAfterAFooterPage_MovesTheSelectionBack()
    {
        var vm = new MainViewModel([_merge, _settings]);
        _settings.IsSelected = true;

        _merge.IsSelected = true;

        Assert.Same(_merge, vm.CurrentPage);
        Assert.Equal([_merge], Selected(_merge, _settings));
    }

    [Fact]
    public void Deselecting_NeverChangesTheCurrentPage()
    {
        var vm = new MainViewModel([_merge, _settings]);

        _merge.IsSelected = false;

        Assert.Same(_merge, vm.CurrentPage);
    }

    [Fact]
    public void TheRadioGroupUncheckingTheOldEntryBeforeCheckingTheNewOne_StillSwitchesPage()
    {
        // The sidebar's radio group can write "old is no longer selected" before
        // "new is selected". The switch must still end on the new page.
        var vm = new MainViewModel([_merge, _settings]);

        _merge.IsSelected = false;
        _settings.IsSelected = true;

        Assert.Same(_settings, vm.CurrentPage);
        Assert.Equal([_settings], Selected(_merge, _settings));
    }

    [Fact]
    public void TheRadioGroupCheckingTheNewEntryBeforeUncheckingTheOldOne_StillSwitchesPage()
    {
        var vm = new MainViewModel([_merge, _settings]);

        _settings.IsSelected = true;
        _merge.IsSelected = false;

        Assert.Same(_settings, vm.CurrentPage);
        Assert.Equal([_settings], Selected(_merge, _settings));
    }

    [Fact]
    public void DeselectingAPageThatIsNotCurrent_ChangesNothing()
    {
        var vm = new MainViewModel([_merge, _split]);

        _split.IsSelected = false;

        Assert.Same(_merge, vm.CurrentPage);
        Assert.Equal([_merge], Selected(_merge, _split));
    }

    [Fact]
    public void SelectingTheCurrentPageAgain_ChangesNothing()
    {
        var vm = new MainViewModel([_merge, _split]);
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        _merge.IsSelected = true;

        Assert.Empty(changes);
        Assert.Same(_merge, vm.CurrentPage);
    }

    [Fact]
    public void CurrentPageChange_IsAnnounced()
    {
        var vm = new MainViewModel([_merge, _split]);
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        _split.IsSelected = true;

        Assert.Equal([nameof(MainViewModel.CurrentPage)], changes);
    }

    [Fact]
    public void OnlyFooterPages_StillShowsAndSelectsAPage()
    {
        var vm = new MainViewModel([_settings]);

        Assert.Same(_settings, vm.CurrentPage);
        Assert.True(_settings.IsSelected);
    }

    [Fact]
    public async Task InitializeAsync_LetsEveryPageDoItsStartUpWork()
    {
        var first = new InitializingPage("First");
        var footer = new InitializingPage("Footer", NavigationPlacement.Footer);
        var vm = new MainViewModel([first, footer]);

        await vm.InitializeAsync();

        Assert.Equal(1, first.InitializeCount);
        Assert.Equal(1, footer.InitializeCount);
    }

    [Fact]
    public async Task InitializeAsync_APageWithNoStartUpWork_Completes()
    {
        var vm = new MainViewModel([_merge, _settings]);

        await vm.InitializeAsync();

        Assert.Same(_merge, vm.CurrentPage);
    }

    [Fact]
    public async Task InitializeAsync_AFailingPage_SurfacesTheError()
    {
        var vm = new MainViewModel([new InitializingPage("Broken") { Failure = new InvalidOperationException("boom") }]);

        await Assert.ThrowsAsync<InvalidOperationException>(vm.InitializeAsync);
    }

    private sealed class InitializingPage(string title, NavigationPlacement placement = NavigationPlacement.Main)
        : PageViewModel(title, "", placement)
    {
        public int InitializeCount { get; private set; }

        public Exception? Failure { get; init; }

        public override Task InitializeAsync()
        {
            InitializeCount++;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    [Fact]
    public void NoPages_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MainViewModel([]));
    }
}
