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

/// <summary>Runs posted actions immediately on the posting thread, so tests see state changes in order and without waiting.</summary>
internal class ImmediateUiThread : IUiThread
{
    public void Post(Action action) => action();
}

/// <summary>Holds posted actions until <see cref="RunAll"/>, like a UI thread that is busy. Safe to post from any thread.</summary>
internal class QueuedUiThread : IUiThread
{
    private readonly Queue<Action> _queue = new();

    public int Pending
    {
        get
        {
            lock (_queue)
                return _queue.Count;
        }
    }

    public void Post(Action action)
    {
        lock (_queue)
            _queue.Enqueue(action);
    }

    /// <summary>Runs everything posted so far, in order, on the calling thread.</summary>
    public void RunAll()
    {
        while (true)
        {
            Action action;
            lock (_queue)
            {
                if (_queue.Count == 0)
                    return;
                action = _queue.Dequeue();
            }

            action();
        }
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

    private readonly Dictionary<string, ManualResetEventSlim> _gates = [];
    private readonly Dictionary<string, Exception> _failures = [];
    private int _findCount;

    /// <summary>Makes <see cref="Find"/> for this folder throw the given exception.</summary>
    public void Fail(string folder, Exception exception) => _failures[folder] = exception;

    /// <summary>How many times <see cref="Find"/> has been called, from any thread.</summary>
    public int FindCount => Volatile.Read(ref _findCount);

    /// <summary>Makes <see cref="Find"/> for this folder block until the returned gate is set, like a slow network share.</summary>
    public ManualResetEventSlim Hold(string folder)
    {
        var gate = new ManualResetEventSlim(false);
        _gates[folder] = gate;
        return gate;
    }

    /// <summary>The managed thread id the last <see cref="Find"/> call ran on.</summary>
    public int LastThreadId { get; private set; }

    public IReadOnlyList<string> Find(string folderPath)
    {
        Interlocked.Increment(ref _findCount);
        LastThreadId = Environment.CurrentManagedThreadId;

        if (_gates.TryGetValue(folderPath, out var gate))
            gate.Wait();

        if (_failures.TryGetValue(folderPath, out var failure))
            throw failure;

        return _folders.TryGetValue(folderPath, out var files)
            ? files
            : throw new SourceFolderNotFoundException(folderPath);
    }
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

    /// <summary>The managed thread id the last <see cref="Merge"/> call ran on.</summary>
    public int LastThreadId { get; private set; }

    /// <summary>Reports made through the progress sink before the merge waits at the gate.</summary>
    public List<MergeProgress> ProgressToReport { get; } = [];

    /// <summary>Set once the progress reports have been delivered.</summary>
    public ManualResetEventSlim Reported { get; } = new(false);

    public MergeResult Merge(MergeOptions options, IProgress<MergeProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(options);
        LastProgress = progress;
        LastToken = cancellationToken;
        LastThreadId = Environment.CurrentManagedThreadId;

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
