using ExoPdf.Core.Errors;
using ExoPdf.Core.Models;
using ExoPdf.Desktop.Services;
using ExoPdf.Desktop.ViewModels;

namespace ExoPdf.Tests;

public class MergeViewModelTests
{
    private const string Folder = @"C:\docs\Invoices";

    private readonly FakePdfMerger _merger = new();
    private readonly FakeMergeSourceFinder _finder = new();
    private readonly FakeFolderPicker _picker = new();
    private readonly FakeShellLauncher _shell = new();
    private readonly FakeSettingsStore _settings = new();

    private MergeViewModel CreateViewModel() => new(_merger, _finder, _picker, _shell, _settings);

    [Fact]
    public void NewViewModel_HasNoFolderAndCannotMerge()
    {
        var vm = CreateViewModel();

        Assert.False(vm.HasFolder);
        Assert.False(vm.HasFiles);
        Assert.False(vm.MergeCommand.CanExecute(null));
    }

    [Fact]
    public void SelectFolder_ListsFilesInTheOrderTheFinderReturnsThem()
    {
        _finder.AddFolder(Folder, "a.pdf", "b.pdf");
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(Folder);

        Assert.Equal(["a.pdf", "b.pdf"], vm.Files.Select(f => f.Name));
        Assert.Equal("2 files", vm.FileCountText);
        Assert.True(vm.MergeCommand.CanExecute(null));
    }

    [Fact]
    public void SelectFolder_SingleFile_UsesSingularCount()
    {
        _finder.AddFolder(Folder, "a.pdf");
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(Folder);

        Assert.Equal("1 file", vm.FileCountText);
    }

    [Fact]
    public void SelectFolder_WithNoPdfs_ShowsEmptyStateAndCannotMerge()
    {
        _finder.AddFolder(Folder);
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(Folder);

        Assert.False(vm.HasFiles);
        Assert.Equal("No PDF files were found in this folder.", vm.EmptyStateText);
        Assert.False(vm.MergeCommand.CanExecute(null));
    }

    [Fact]
    public void SelectFolder_Missing_ShowsErrorAndDoesNotRememberIt()
    {
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(Folder);

        Assert.True(vm.HasError);
        Assert.Empty(vm.Files);
        Assert.Equal("The PDF files in this folder could not be listed.", vm.EmptyStateText);
        Assert.False(vm.MergeCommand.CanExecute(null));
        Assert.Null(_settings.Current.LastMergeFolder);
        Assert.Equal(0, _settings.SaveCount);
    }

    [Fact]
    public void SelectFolder_BlankPath_IsIgnored()
    {
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute("  ");

        Assert.False(vm.HasFolder);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void SelectFolder_SameFolderAgain_RefreshesList()
    {
        _finder.AddFolder(Folder, "a.pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        _finder.AddFolder(Folder, "a.pdf", "b.pdf");
        vm.SelectFolderCommand.Execute(Folder);

        Assert.Equal(2, vm.Files.Count);
    }

    [Fact]
    public void SelectFolder_RemembersFolderInSettings()
    {
        _finder.AddFolder(Folder, "a.pdf");
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(Folder);

        Assert.Equal(Folder, _settings.Current.LastMergeFolder);
        Assert.Equal(1, _settings.SaveCount);
    }

    [Fact]
    public void NewViewModel_RestoresLastFolderIfItCanStillBeListed()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _settings.Current.LastMergeFolder = Folder;

        var vm = CreateViewModel();

        Assert.Equal(Folder, vm.SourceFolder);
        Assert.Single(vm.Files);
        Assert.Equal(0, _settings.SaveCount);
    }

    [Fact]
    public void NewViewModel_DropsLastFolderThatNoLongerExists_WithoutAnError()
    {
        _settings.Current.LastMergeFolder = Folder;

        var vm = CreateViewModel();

        Assert.False(vm.HasFolder);
        Assert.False(vm.HasError);
        Assert.Equal("Choose a folder to see the PDFs that will be merged.", vm.EmptyStateText);
    }

    [Fact]
    public void Browse_PickedFolder_IsSelected()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _picker.Result = Folder;
        var vm = CreateViewModel();

        vm.BrowseCommand.Execute(null);

        Assert.Equal(Folder, vm.SourceFolder);
        Assert.Single(vm.Files);
    }

    [Fact]
    public void Browse_Cancelled_KeepsCurrentFolderAndOffersItToThePicker()
    {
        _finder.AddFolder(Folder, "a.pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        _picker.Result = null;

        vm.BrowseCommand.Execute(null);

        Assert.Equal(Folder, vm.SourceFolder);
        Assert.Equal(Folder, _picker.LastInitialFolder);
    }

    [Fact]
    public async Task Merge_Success_MergesTheSelectedFolderAndShowsTheResult()
    {
        _finder.AddFolder(Folder, "a.pdf", "b.pdf");
        _merger.Result = new MergeResult { OutputFilePath = Path.Combine(Folder, "out.pdf"), FilesMerged = 2, TotalPages = 5 };
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.Equal(Folder, Assert.Single(_merger.Calls).SourceFolderPath);
        Assert.Equal("Merged 2 files (5 pages)", vm.ResultSummary);
        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Merge_SingleFileSinglePage_UsesSingularWords()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Result = new MergeResult { OutputFilePath = "x.pdf", FilesMerged = 1, TotalPages = 1 };
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.Equal("Merged 1 file (1 page)", vm.ResultSummary);
    }

    [Fact]
    public async Task Merge_WhileRunning_IsBusyAndDisablesCommands()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Gate = new ManualResetEventSlim(false);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        var running = vm.MergeCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.MergeCommand.CanExecute(null));
        Assert.False(vm.BrowseCommand.CanExecute(null));
        Assert.False(vm.SelectFolderCommand.CanExecute(Folder));

        _merger.Gate.Set();
        await running;

        Assert.False(vm.IsBusy);
        Assert.True(vm.BrowseCommand.CanExecute(null));
    }

    [Fact]
    public async Task Merge_Fails_ShowsTheMessageInsteadOfThrowing()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Exception = new PdfUnreadableException(@"C:\docs\Invoices\a.pdf", new InvalidOperationException("bad header"));
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.Equal("Cannot read \"a.pdf\": bad header", vm.ErrorMessage);
        Assert.Null(vm.Result);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Merge_UnexpectedFailure_IsNotHiddenButStillEndsTheBusyState()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Exception = new NullReferenceException("a bug");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        await Assert.ThrowsAsync<NullReferenceException>(() => vm.MergeCommand.ExecuteAsync(null));

        Assert.False(vm.IsBusy);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task Merge_AfterAFailure_ClearsTheOldError()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Exception = new NoPdfFilesException(Folder);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        await vm.MergeCommand.ExecuteAsync(null);

        _merger.Exception = null;
        await vm.MergeCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.True(vm.HasResult);
    }

    [Fact]
    public async Task SelectingAnotherFolder_ClearsPreviousResult()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _finder.AddFolder(@"C:\docs\Other", "b.pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        await vm.MergeCommand.ExecuteAsync(null);

        vm.SelectFolderCommand.Execute(@"C:\docs\Other");

        Assert.Null(vm.Result);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public async Task OpenResult_LaunchesOutputFile()
    {
        _finder.AddFolder(Folder, "a.pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        await vm.MergeCommand.ExecuteAsync(null);

        vm.OpenResultCommand.Execute(null);
        vm.ShowResultInFolderCommand.Execute(null);

        Assert.Equal([vm.Result!.OutputFilePath], _shell.OpenedFiles);
        Assert.Equal([vm.Result.OutputFilePath], _shell.ShownInFolder);
    }

    [Fact]
    public async Task OpenResult_LauncherFails_ShowsError()
    {
        _finder.AddFolder(Folder, "a.pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        await vm.MergeCommand.ExecuteAsync(null);
        _shell.ThrowOnLaunch = new ShellLaunchException("no viewer");

        vm.OpenResultCommand.Execute(null);

        Assert.Equal("no viewer", vm.ErrorMessage);
    }

    [Fact]
    public void OpenResult_BeforeAnyMerge_DoesNothing()
    {
        var vm = CreateViewModel();

        vm.OpenResultCommand.Execute(null);

        Assert.Empty(_shell.OpenedFiles);
    }
}
