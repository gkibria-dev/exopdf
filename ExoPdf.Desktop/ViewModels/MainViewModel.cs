using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ExoPdf.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private PageViewModel _currentPage;

    public MainViewModel(IEnumerable<PageViewModel> pages)
    {
        var all = pages.ToList();
        Pages = all.Where(p => p.Placement == NavigationPlacement.Main).ToList();
        FooterPages = all.Where(p => p.Placement == NavigationPlacement.Footer).ToList();

        _currentPage = Pages.FirstOrDefault() ?? FooterPages.FirstOrDefault()
            ?? throw new ArgumentException("At least one page must be registered.", nameof(pages));
        _currentPage.IsSelected = true;

        foreach (var page in all)
            page.PropertyChanged += OnPagePropertyChanged;
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    public IReadOnlyList<PageViewModel> FooterPages { get; }

    // CurrentPage is the single source of truth; each page's IsSelected follows it.
    partial void OnCurrentPageChanged(PageViewModel? oldValue, PageViewModel newValue)
    {
        if (oldValue is not null)
            oldValue.IsSelected = false;

        newValue.IsSelected = true;
    }

    // The sidebar writes IsSelected when the user picks an entry (by mouse or arrow keys).
    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PageViewModel.IsSelected) || sender is not PageViewModel page)
            return;

        if (page.IsSelected)
            CurrentPage = page;
        else if (page == CurrentPage)
            page.IsSelected = true; // the page being shown cannot be deselected
    }
}
