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

internal class FakeSettingsService : ISettingsService
{
    /// <summary>Set this in a test to preload settings.</summary>
    public AppSettings Current { get; set; } = new();

    public int UpdateCount { get; private set; }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        UpdateCount++;
    }
}

internal class FakeSettingsStore : ISettingsStore
{
    /// <summary>What <see cref="Load"/> returns.</summary>
    public AppSettings Stored { get; set; } = new();

    public int LoadCount { get; private set; }

    public List<AppSettings> Saved { get; } = [];

    public AppSettings Load()
    {
        LoadCount++;
        return Stored;
    }

    public void Save(AppSettings settings)
    {
        Saved.Add(settings);
        Stored = settings;
    }
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

    /// <summary>The progress sink and token the last call received.</summary>
    public IProgress<MergeProgress>? LastProgress { get; private set; }

    public CancellationToken LastToken { get; private set; }

    /// <summary>Reports made through the progress sink before the merge waits at the gate.</summary>
    public List<MergeProgress> ProgressToReport { get; } = [];

    /// <summary>Set once the progress reports have been delivered.</summary>
    public ManualResetEventSlim Reported { get; } = new(false);

    public MergeResult Merge(MergeOptions options, IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(options);
        LastProgress = progress;
        LastToken = cancellationToken;

        foreach (var report in ProgressToReport)
            progress?.Report(report);
        Reported.Set();

        Gate?.Wait(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

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

/// <summary>A synchronous <see cref="IProgress{T}"/>: reports are recorded on the calling thread, in order.</summary>
internal class RecordingProgress<T> : IProgress<T>
{
    private readonly Action<T>? _onReport;

    public RecordingProgress(Action<T>? onReport = null) => _onReport = onReport;

    public List<T> Reports { get; } = [];

    public void Report(T value)
    {
        Reports.Add(value);
        _onReport?.Invoke(value);
    }
}
