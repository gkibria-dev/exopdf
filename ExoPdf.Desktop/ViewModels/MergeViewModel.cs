using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExoPdf.Core.Models;
using ExoPdf.Core.Operations;
using ExoPdf.Desktop.Services;

namespace ExoPdf.Desktop.ViewModels;

public partial class MergeViewModel : PageViewModel
{
    private readonly PdfMerger _merger;
    private readonly IFolderPicker _folderPicker;
    private readonly IShellLauncher _shell;
    private readonly ISettingsStore _settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolder))]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private string? _sourceFolder;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MergeCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    [NotifyCanExecuteChangedFor(nameof(SelectFolderCommand))]
    [NotifyPropertyChangedFor(nameof(CanChangeFolder))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(ResultSummary))]
    private MergeResult? _result;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(EmptyStateText))]
    private string? _errorMessage;

    public MergeViewModel(PdfMerger merger, IFolderPicker folderPicker, IShellLauncher shell, ISettingsStore settings)
        : base("Merge", "\uE8C8")
    {
        _merger = merger;
        _folderPicker = folderPicker;
        _shell = shell;
        _settings = settings;

        var lastFolder = settings.Current.LastMergeFolder;
        if (!string.IsNullOrEmpty(lastFolder) && Directory.Exists(lastFolder))
            SourceFolder = lastFolder;
    }

    /// <summary>The PDFs that will be merged, in merge order.</summary>
    public ObservableCollection<MergeFileItem> Files { get; } = [];

    public bool HasFolder => !string.IsNullOrEmpty(SourceFolder);

    public bool HasFiles => Files.Count > 0;

    public bool HasResult => Result is not null;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanChangeFolder => !IsBusy;

    public string EmptyStateText =>
        !HasFolder ? "Choose a folder to see the PDFs that will be merged."
        : HasError ? "The PDF files in this folder could not be listed."
        : "No PDF files were found in this folder.";

    public string FileCountText => Files.Count == 1 ? "1 file" : $"{Files.Count} files";

    public string? ResultSummary => Result is null
        ? null
        : $"Merged {Result.FilesMerged} {(Result.FilesMerged == 1 ? "file" : "files")} ({Result.TotalPages} {(Result.TotalPages == 1 ? "page" : "pages")})";

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

        if (Directory.Exists(path))
        {
            _settings.Current.LastMergeFolder = path;
            _settings.Save();
        }
    }

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        var options = new MergeOptions { SourceFolderPath = SourceFolder! };

        IsBusy = true;
        ErrorMessage = null;
        Result = null;

        try
        {
            Result = await Task.Run(() => _merger.Merge(options));
        }
        catch (Exception ex)
        {
            // Last line of defence at the UI boundary: whatever went wrong (locked or
            // corrupt file, permissions), show it rather than crash.
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanMerge() => HasFiles && !IsBusy;

    [RelayCommand]
    private void OpenResult()
    {
        if (Result is { } result)
            Launch(() => _shell.OpenFile(result.OutputFilePath));
    }

    [RelayCommand]
    private void ShowResultInFolder()
    {
        if (Result is { } result)
            Launch(() => _shell.ShowInFolder(result.OutputFilePath));
    }

    private void Launch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ReloadFolder()
    {
        Result = null;
        ErrorMessage = null;
        RefreshFiles();
    }

    private void RefreshFiles()
    {
        Files.Clear();

        if (HasFolder)
        {
            try
            {
                foreach (var file in _merger.GetSourceFiles(SourceFolder!))
                    Files.Add(new MergeFileItem(file));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ErrorMessage = ex.Message;
            }
        }

        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(FileCountText));
        MergeCommand.NotifyCanExecuteChanged();
    }
}
