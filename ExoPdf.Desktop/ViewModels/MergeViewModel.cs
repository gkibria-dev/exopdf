using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExoPdf.Core.Errors;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Models;
using ExoPdf.Desktop.Services;

namespace ExoPdf.Desktop.ViewModels;

public partial class MergeViewModel : PageViewModel
{
    private const int MaxRecentFolders = 5;

    // Stale means a newer pick already took over before this one finished; it is not a
    // failure of the folder itself, so Recents must not be touched because of it.
    private enum LoadOutcome { Loaded, Stale, Failed }

    private readonly IPdfMerger _merger;
    private readonly IMergeSourceFinder _finder;
    private readonly IFolderPicker _folderPicker;
    private readonly IShellLauncher _shell;
    private readonly ISettingsService _settings;
    private readonly IUiThread _uiThread;

    private CancellationTokenSource? _cancellation;

    // Each folder listing takes the next number. A listing that finishes after a newer
    // one has started is stale and its result is ignored (the file-system call itself
    // cannot be cancelled, only abandoned).
    private int _listingVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolder))]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private string? _sourceFolder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MergeCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFolderCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyPropertyChangedFor(nameof(CanChangeFolder))]
    private bool _isBusy;

    /// <summary>True once every file has been merged and the output is being written; that cannot be cancelled.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isSaving;

    /// <summary>True while the files of the selected folder are being listed.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MergeCommand))]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    [NotifyPropertyChangedFor(nameof(FileCountText))]
    private bool _isLoadingFiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    private MergeResultViewModel? _result;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _noticeMessage;

    [ObservableProperty]
    private int _filesCompleted;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private string _progressText = "";

    public MergeViewModel(
        IPdfMerger merger,
        IMergeSourceFinder finder,
        IFolderPicker folderPicker,
        IShellLauncher shell,
        ISettingsService settings,
        IUiThread uiThread)
        : base("Merge", "\uE8C8")
    {
        _merger = merger;
        _finder = finder;
        _folderPicker = folderPicker;
        _shell = shell;
        _settings = settings;
        _uiThread = uiThread;
    }

    /// <summary>
    /// Populates the Recents list from settings. Touches only settings, never the file
    /// system, so a folder that is gone or unreachable cannot affect start-up.
    /// </summary>
    public override Task InitializeAsync()
    {
        RefreshRecentFolders();
        return Task.CompletedTask;
    }

    /// <summary>The PDFs that will be merged, in merge order.</summary>
    public ObservableCollection<MergeFileItem> Files { get; } = [];

    /// <summary>Folders used in Merge before, most recent first. Pick one to reopen it.</summary>
    public ObservableCollection<string> RecentFolders { get; } = [];

    public bool HasRecentFolders => RecentFolders.Count > 0;

    public bool HasFolder => !string.IsNullOrEmpty(SourceFolder);

    public bool HasFiles => Files.Count > 0;

    public bool HasResult => Result is not null;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>A neutral message that is not an error, such as "Merge cancelled."</summary>
    public bool HasNotice => !string.IsNullOrEmpty(NoticeMessage);

    public bool CanChangeFolder => !IsBusy;

    public string EmptyStateText =>
        !HasFolder ? "Choose a folder to see the PDFs that will be merged."
        : IsLoadingFiles ? "Reading the folder…"
        : HasError ? "The PDF files in this folder could not be listed."
        : "No PDF files were found in this folder.";

    public string FileCountText =>
        IsLoadingFiles ? ""
        : Files.Count == 1 ? "1 file"
        : $"{Files.Count} files";

    // Concurrent executions are allowed so the user can pick another folder while a slow
    // one is still being listed; the newest choice wins.
    [RelayCommand(CanExecute = nameof(CanChangeFolder), AllowConcurrentExecutions = true)]
    private async Task BrowseAsync()
    {
        var folder = _folderPicker.PickFolder(SourceFolder);
        if (folder is not null)
            await SelectFolderAsync(folder);
    }

    /// <summary>Sets the source folder, for example from a drag-and-drop.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeFolder), AllowConcurrentExecutions = true)]
    private async Task SelectFolderAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        await LoadFolderAsync(path);
    }

    /// <summary>Reopens a folder the user picked from Recents.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeFolder), AllowConcurrentExecutions = true)]
    private async Task OpenRecentFolderAsync(string path)
    {
        // Loaded: left as is, merging is what moves a folder to the front of Recents.
        // Stale: a newer pick already took over; the folder itself was never shown to be
        // bad, so it must be left in Recents too.
        if (await LoadFolderAsync(path) == LoadOutcome.Failed)
            ForgetFolder(path);
    }

    /// <summary>Moves <paramref name="path"/> to the front of Recents, capped and de-duplicated.</summary>
    private void RememberFolder(string path)
    {
        var updated = new[] { path }
            .Concat(_settings.Current.RecentMergeFolders.Where(f => !string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxRecentFolders)
            .ToList();

        // A new List<string> never equals the old one by SettingsService's reference-based
        // no-op check, so skip the update (and the save it would trigger) here instead.
        if (!updated.SequenceEqual(_settings.Current.RecentMergeFolders))
            _settings.Update(s => s with { RecentMergeFolders = updated });

        RefreshRecentFolders();
    }

    /// <summary>Removes a folder from Recents, for example one that can no longer be listed.</summary>
    private void ForgetFolder(string path)
    {
        var updated = _settings.Current.RecentMergeFolders
            .Where(f => !string.Equals(f, path, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!updated.SequenceEqual(_settings.Current.RecentMergeFolders))
            _settings.Update(s => s with { RecentMergeFolders = updated });

        RefreshRecentFolders();
    }

    private void RefreshRecentFolders()
    {
        RecentFolders.Clear();
        foreach (var folder in _settings.Current.RecentMergeFolders)
            RecentFolders.Add(folder);

        OnPropertyChanged(nameof(HasRecentFolders));
    }

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        // Merge exactly the files the user was shown, not whatever the folder holds by now.
        var options = new MergeOptions
        {
            SourceFolderPath = SourceFolder!,
            Files = Files.Select(file => file.FullPath).ToList()
        };

        IsBusy = true;
        IsSaving = false;
        ErrorMessage = null;
        NoticeMessage = null;
        Result = null;
        FilesCompleted = 0;
        TotalFiles = Files.Count;
        ProgressText = "Starting…";

        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;

        try
        {
            // Reports arrive on the merging thread and are posted to the UI thread, where
            // OnProgress may safely change command state; WPF does not marshal that.
            // Posts run in order, and all of them are queued before this method resumes.
            var progress = new DirectProgress<MergeProgress>(report => _uiThread.Post(() => OnProgress(report)));
            var result = await Task.Run(() => _merger.Merge(options, progress, cancellation.Token), cancellation.Token);
            Result = new MergeResultViewModel(result, _shell);

            // A folder only earns a place in Recents once it has actually been merged,
            // not just browsed to.
            RememberFolder(options.SourceFolderPath);
        }
        catch (OperationCanceledException)
        {
            NoticeMessage = "Merge cancelled.";
        }
        catch (ExoPdfException ex)
        {
            // An expected failure: show it. Anything else is a bug and reaches the
            // application's unexpected-error dialog.
            ErrorMessage = ex.Message;
        }
        finally
        {
            _cancellation = null;
            IsSaving = false;
            IsBusy = false;
        }
    }

    private bool CanMerge() => HasFiles && !IsBusy && !IsLoadingFiles;

    // Once saving has started the token is no longer checked, so Cancel would do nothing.
    private bool CanCancel() => IsBusy && !IsSaving;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cancellation?.Cancel();

    private void OnProgress(MergeProgress progress)
    {
        // A report posted just before the merge ended can arrive after it; drop it.
        if (!IsBusy)
            return;

        FilesCompleted = progress.FilesCompleted;
        TotalFiles = progress.TotalFiles;

        if (progress.Stage == MergeStage.Saving)
        {
            IsSaving = true;
            ProgressText = "Saving merged file…";
        }
        else
        {
            ProgressText = $"Merged {progress.FilesCompleted} of {progress.TotalFiles}: {progress.CurrentFile}";
        }
    }

    /// <summary>
    /// Makes <paramref name="folder"/> the source folder and lists its files off the UI
    /// thread.
    /// </summary>
    private async Task<LoadOutcome> LoadFolderAsync(string folder)
    {
        var version = ++_listingVersion;

        SourceFolder = folder;
        Result = null;
        ErrorMessage = null;
        NoticeMessage = null;
        SetFiles([]);
        IsLoadingFiles = true;

        try
        {
            var found = await Task.Run(() => _finder.Find(folder));

            // A newer listing has started; it owns the state now.
            if (version != _listingVersion)
                return LoadOutcome.Stale;

            SetFiles(found);
            return LoadOutcome.Loaded;
        }
        catch (ExoPdfException ex)
        {
            if (version != _listingVersion)
                return LoadOutcome.Stale;

            ErrorMessage = ex.Message;
            return LoadOutcome.Failed;
        }
        finally
        {
            // Also runs when something unexpected escapes, so "Reading the folder…" can
            // never be left showing.
            if (version == _listingVersion)
                IsLoadingFiles = false;
        }
    }

    private void SetFiles(IEnumerable<string> paths)
    {
        Files.Clear();
        foreach (var path in paths)
            Files.Add(new MergeFileItem(path));

        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(FileCountText));
        MergeCommand.NotifyCanExecuteChanged();
    }
}
