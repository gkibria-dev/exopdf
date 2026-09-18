using CommunityToolkit.Mvvm.ComponentModel;

namespace ExoPdf.Desktop.ViewModels;

public enum NavigationPlacement
{
    /// <summary>Listed with the operations at the top of the sidebar.</summary>
    Main,

    /// <summary>Pinned to the bottom of the sidebar.</summary>
    Footer
}

/// <summary>
/// A page the user can navigate to from the sidebar. Every PDF operation, and
/// Settings, derives from this and is registered with the DI container.
/// </summary>
public abstract class PageViewModel : ObservableObject
{
    protected PageViewModel(string title, string iconGlyph, NavigationPlacement placement = NavigationPlacement.Main)
    {
        Title = title;
        IconGlyph = iconGlyph;
        Placement = placement;
    }

    public string Title { get; }

    /// <summary>A code point from Segoe Fluent Icons / Segoe MDL2 Assets.</summary>
    public string IconGlyph { get; }

    public NavigationPlacement Placement { get; }

    /// <summary>
    /// Work a page does when the application starts, awaited after the window is shown so
    /// that slow work cannot delay it. Override where needed.
    /// </summary>
    public virtual Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Whether this is the page being shown. Bound two-way to the sidebar entry;
    /// <see cref="MainViewModel"/> keeps exactly one page selected.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isSelected;
}
