using CommunityToolkit.Mvvm.ComponentModel;

namespace ExoPdf.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private PageViewModel _currentPage;

    [ObservableProperty]
    private PageViewModel? _selectedPage;

    [ObservableProperty]
    private PageViewModel? _selectedFooterPage;

    public MainViewModel(IEnumerable<PageViewModel> pages)
    {
        var all = pages.ToList();
        Pages = all.Where(p => p.Placement == NavigationPlacement.Main).ToList();
        FooterPages = all.Where(p => p.Placement == NavigationPlacement.Footer).ToList();

        _currentPage = all.FirstOrDefault()
            ?? throw new ArgumentException("At least one page must be registered.", nameof(pages));
        SelectedPage = Pages.FirstOrDefault();
        SelectedFooterPage = SelectedPage is null ? FooterPages.FirstOrDefault() : null;
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    public IReadOnlyList<PageViewModel> FooterPages { get; }

    // The two sidebar lists share one selection: choosing in one clears the other.
    // A null value is either the other list being cleared (ignored) or the user
    // deselecting the current entry with Ctrl+click, in which case it is restored
    // so the sidebar always agrees with the page shown.
    partial void OnSelectedPageChanged(PageViewModel? value)
    {
        if (value is null)
        {
            if (SelectedFooterPage is null)
                RestoreSelection();
            return;
        }

        SelectedFooterPage = null;
        CurrentPage = value;
    }

    partial void OnSelectedFooterPageChanged(PageViewModel? value)
    {
        if (value is null)
        {
            if (SelectedPage is null)
                RestoreSelection();
            return;
        }

        SelectedPage = null;
        CurrentPage = value;
    }

    private void RestoreSelection()
    {
        if (Pages.Contains(CurrentPage))
            SelectedPage = CurrentPage;
        else
            SelectedFooterPage = CurrentPage;
    }
}
