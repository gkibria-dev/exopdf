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

        RestoreLastFolder();
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
        : HasError ? "The PDF files in this folder could not be listed."
        : "No PDF files were found in this folder.";

    public string FileCountText => Files.Count == 1 ? "1 file" : $"{Files.Count} files";

    partial void OnSourceFolderChanged(string? value) => ReloadFolder();

    [RelayCommand(CanExecute = nameof(CanChangeFolder))]
    private void Browse()
    {
        var folder = _folderPicker.PickFolder(SourceFolder);
        if (folder is not null)
            SelectFolder(folder);
    }

    /// <summary>Sets the source folder, for example from a drag-and-drop.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeFolder))]
    private void SelectFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (path == SourceFolder)
            ReloadFolder(); // picking the same folder again refreshes the list
        else
            SourceFolder = path;

        // Only a folder that could be listed is worth offering again next time.
        if (!HasError)
        {
            _settings.Update(s => s with { LastMergeFolder = path });
        }
    }

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        var options = new MergeOptions { SourceFolderPath = SourceFolder! };

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

    private bool CanMerge() => HasFiles && !IsBusy;

    [RelayCommand(CanExecute = nameof(IsBusy))]
    private void Cancel() => _cancellation?.Cancel();

    private void OnProgress(MergeProgress progress)
    {
        FilesCompleted = progress.FilesCompleted;
        TotalFiles = progress.TotalFiles;
        ProgressText = $"Merged {progress.FilesCompleted} of {progress.TotalFiles}: {progress.CurrentFile}";
    }

    /// <summary>Offers the last used folder again, silently dropping it if it can no longer be listed.</summary>
    private void RestoreLastFolder()
    {
        var lastFolder = _settings.Current.LastMergeFolder;
        if (string.IsNullOrEmpty(lastFolder))
            return;

        SourceFolder = lastFolder;
        if (HasError)
            SourceFolder = null;
    }

    private void ReloadFolder()
    {
        Result = null;
        ErrorMessage = null;
        NoticeMessage = null;
        RefreshFiles();
    }

    private void RefreshFiles()
    {
        Files.Clear();

        if (HasFolder)
        {
            try
            {
                foreach (var file in _finder.Find(SourceFolder!))
                    Files.Add(new MergeFileItem(file));
            }
            catch (ExoPdfException ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(FileCountText));
        MergeCommand.NotifyCanExecuteChanged();
    }
}
