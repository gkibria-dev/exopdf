using ExoPdf.Core.Errors;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Models;
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

internal class FakeMergeSourceFinder : IMergeSourceFinder
{
    private readonly Dictionary<string, List<string>> _folders = [];

    /// <summary>Registers a folder and the file names the finder reports for it, in that order.</summary>
    public void AddFolder(string folder, params string[] fileNames) =>
        _folders[folder] = fileNames.Select(name => Path.Combine(folder, name)).ToList();

    public IReadOnlyList<string> Find(string folderPath) =>
        _folders.TryGetValue(folderPath, out var files)
            ? files
            : throw new SourceFolderNotFoundException(folderPath);
}

internal class FakePdfMerger : IPdfMerger
{
    public List<MergeOptions> Calls { get; } = [];

    /// <summary>Returned by <see cref="Merge"/>; a default result is made up when null.</summary>
    public MergeResult? Result { get; set; }

    public Exception? Exception { get; set; }

    /// <summary>When set, <see cref="Merge"/> blocks until the gate is opened.</summary>
    public ManualResetEventSlim? Gate { get; set; }

    public MergeResult Merge(MergeOptions options)
    {
        Calls.Add(options);
        Gate?.Wait();

        if (Exception is not null)
            throw Exception;

        return Result ?? new MergeResult
        {
            OutputFilePath = Path.Combine(options.SourceFolderPath, "Merged.pdf"),
            FilesMerged = 1,
            TotalPages = 1
        };
    }
}
