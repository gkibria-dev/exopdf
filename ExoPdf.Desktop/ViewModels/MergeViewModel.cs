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
    private readonly IPdfMerger _merger;
    private readonly IMergeSourceFinder _finder;
    private readonly IFolderPicker _folderPicker;
    private readonly IShellLauncher _shell;
    private readonly ISettingsService _settings;

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

    public MergeViewModel(IPdfMerger merger, IMergeSourceFinder finder, IFolderPicker folderPicker, IShellLauncher shell, ISettingsService settings)
        : base("Merge", "\uE8C8")
    {
        _merger = merger;
        _finder = finder;
        _folderPicker = folderPicker;
        _shell = shell;
        _settings = settings;
    }

    /// <summary>
    /// Offers the last used folder again. Runs after the window is shown, so a slow
    /// folder cannot delay startup.
    /// </summary>
    public override async Task InitializeAsync()
    {
        var lastFolder = _settings.Current.LastMergeFolder;
        if (string.IsNullOrEmpty(lastFolder))
            return;

        await LoadFolderAsync(lastFolder);

        // A folder that can no longer be listed is dropped without an error. If the user
        // has chosen something else in the meantime, leave their choice alone.
        if (SourceFolder == lastFolder && HasError)
        {
            SourceFolder = null;
            ErrorMessage = null;
        }
    }

    /// <summary>The PDFs that will be merged, in merge order.</summary>
    public ObservableCollection<MergeFileItem> Files { get; } = [];

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

        // Only a folder that could be listed is worth offering again next time.
        if (await LoadFolderAsync(path))
            _settings.Update(s => s with { LastMergeFolder = path });
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
            // Reports are handled directly on the merging thread: WPF bindings marshal
            // property changes to the UI thread, and tests see them in order.
            var progress = new DirectProgress<MergeProgress>(OnProgress);
            var result = await Task.Run(() => _merger.Merge(options, progress, cancellation.Token), cancellation.Token);
            Result = new MergeResultViewModel(result, _shell);
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
            IsBusy = false;
        }
    }

    private bool CanMerge() => HasFiles && !IsBusy && !IsLoadingFiles;

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cancellation?.Cancel();

    private void OnProgress(MergeProgress progress)
    {
        FilesCompleted = progress.FilesCompleted;
        TotalFiles = progress.TotalFiles;
        ProgressText = $"Merged {progress.FilesCompleted} of {progress.TotalFiles}: {progress.CurrentFile}";
    }

    /// <summary>
    /// Makes <paramref name="folder"/> the source folder and lists its files off the UI
    /// thread. Returns true if the listing succeeded and is still the newest one.
    /// </summary>
    private async Task<bool> LoadFolderAsync(string folder)
    {
        var version = ++_listingVersion;

        SourceFolder = folder;
        Result = null;
        ErrorMessage = null;
        NoticeMessage = null;
        SetFiles([]);
        IsLoadingFiles = true;

        IReadOnlyList<string> found;
        try
        {
            found = await Task.Run(() => _finder.Find(folder));
        }
        catch (ExoPdfException ex)
        {
            if (version != _listingVersion)
                return false;

            ErrorMessage = ex.Message;
            IsLoadingFiles = false;
            return false;
        }

        // A newer listing has started; it owns the state now.
        if (version != _listingVersion)
            return false;

        SetFiles(found);
        IsLoadingFiles = false;
        return true;
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
