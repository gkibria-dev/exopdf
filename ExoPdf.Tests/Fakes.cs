using ExoPdf.Desktop.Models;
using ExoPdf.Desktop.Services;
using ExoPdf.Desktop.ViewModels;

namespace ExoPdf.Tests;

internal class FakeFolderPicker : IFolderPicker
{
    public string? Result { get; set; }

    public string? LastInitialFolder { get; private set; }

    public string? PickFolder(string? initialFolder)
    {
        LastInitialFolder = initialFolder;
        return Result;
    }
}

internal class FakeShellLauncher : IShellLauncher
{
    public List<string> OpenedFiles { get; } = [];

    public List<string> ShownInFolder { get; } = [];

    public Exception? ThrowOnLaunch { get; set; }

    public void OpenFile(string filePath)
    {
        if (ThrowOnLaunch is not null)
            throw ThrowOnLaunch;
        OpenedFiles.Add(filePath);
    }

    public void ShowInFolder(string filePath)
    {
        if (ThrowOnLaunch is not null)
            throw ThrowOnLaunch;
        ShownInFolder.Add(filePath);
    }
}

internal class FakeThemeService : IThemeService
{
    public List<AppTheme> Applied { get; } = [];

    public void Apply(AppTheme theme) => Applied.Add(theme);
}

internal class FakeSettingsStore : ISettingsStore
{
    public AppSettings Current { get; } = new();

    public int SaveCount { get; private set; }

    public void Save() => SaveCount++;
}

internal class TestPage : PageViewModel
{
    public TestPage(string title, NavigationPlacement placement = NavigationPlacement.Main)
        : base(title, "", placement)
    {
    }
}
