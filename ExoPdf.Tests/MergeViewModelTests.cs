using ExoPdf.Core.Errors;
using ExoPdf.Core.Models;
using ExoPdf.Desktop.Models;
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
    private readonly FakeSettingsService _settings = new();

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
        Assert.Equal(0, _settings.UpdateCount);
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
        Assert.Equal(1, _settings.UpdateCount);
    }

    [Fact]
    public void NewViewModel_RestoresLastFolderIfItCanStillBeListed()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _settings.Current = new AppSettings { LastMergeFolder = Folder };

        var vm = CreateViewModel();

        Assert.Equal(Folder, vm.SourceFolder);
        Assert.Single(vm.Files);
        Assert.Equal(0, _settings.UpdateCount);
    }

    [Fact]
    public void NewViewModel_DropsLastFolderThatNoLongerExists_WithoutAnError()
    {
        _settings.Current = new AppSettings { LastMergeFolder = Folder };

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
        Assert.Equal("Merged 2 files (5 pages)", vm.Result!.Summary);
        Assert.Equal(Path.Combine(Folder, "out.pdf"), vm.Result.OutputFilePath);
        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Merge_MergesExactlyTheFilesShown_InTheShownOrder()
    {
        _finder.AddFolder(Folder, "b.pdf", "a.pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        // The folder changes after the preview; the merge must still use the preview.
        _finder.AddFolder(Folder, "a.pdf", "b.pdf", "surprise.pdf");
        await vm.MergeCommand.ExecuteAsync(null);

        var options = Assert.Single(_merger.Calls);
        Assert.Equal(Folder, options.SourceFolderPath);
        Assert.Equal(
            [Path.Combine(Folder, "b.pdf"), Path.Combine(Folder, "a.pdf")],
            options.Files);
    }

    [Fact]
    public async Task Merge_SingleFileSinglePage_UsesSingularWords()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Result = new MergeResult { OutputFilePath = "x.pdf", FilesMerged = 1, TotalPages = 1 };
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.Equal("Merged 1 file (1 page)", vm.Result!.Summary);
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
    public async Task Merge_Result_OpensThroughTheShellLauncher()
    {
        _finder.AddFolder(Folder, "a.pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        await vm.MergeCommand.ExecuteAsync(null);

        vm.Result!.OpenCommand.Execute(null);

        Assert.Equal([vm.Result.OutputFilePath], _shell.OpenedFiles);
    }

    // --- progress -----------------------------------------------------------

    [Fact]
    public async Task Merge_StartsWithZeroProgressOutOfTheListedFiles()
    {
        _finder.AddFolder(Folder, "a.pdf", "b.pdf", "c.pdf");
        _merger.Gate = new ManualResetEventSlim(false);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        var running = vm.MergeCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.FilesCompleted);
        Assert.Equal(3, vm.TotalFiles);
        Assert.Equal("Starting…", vm.ProgressText);

        _merger.Gate.Set();
        await running;
    }

    [Fact]
    public async Task Merge_ShowsTheProgressTheMergerReports()
    {
        _finder.AddFolder(Folder, "a.pdf", "b.pdf", "c.pdf");
        _merger.ProgressToReport.Add(new MergeProgress(1, 3, "a.pdf"));
        _merger.ProgressToReport.Add(new MergeProgress(2, 3, "b.pdf"));
        _merger.Gate = new ManualResetEventSlim(false);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        var running = vm.MergeCommand.ExecuteAsync(null);
        Assert.True(_merger.Reported.Wait(TimeSpan.FromSeconds(5)));

        Assert.Equal(2, vm.FilesCompleted);
        Assert.Equal(3, vm.TotalFiles);
        Assert.Equal("Merged 2 of 3: b.pdf", vm.ProgressText);

        _merger.Gate.Set();
        await running;
    }

    // --- cancellation -------------------------------------------------------

    [Fact]
    public async Task Cancel_IsOnlyPossibleWhileMerging()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Gate = new ManualResetEventSlim(false);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        Assert.False(vm.CancelCommand.CanExecute(null));

        var running = vm.MergeCommand.ExecuteAsync(null);
        Assert.True(vm.CancelCommand.CanExecute(null));

        _merger.Gate.Set();
        await running;
        Assert.False(vm.CancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_StopsTheMerge_ShowsANeutralNotice_AndNoResultOrError()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Gate = new ManualResetEventSlim(false);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        var running = vm.MergeCommand.ExecuteAsync(null);
        vm.CancelCommand.Execute(null);
        await running;

        Assert.Equal("Merge cancelled.", vm.NoticeMessage);
        Assert.True(vm.HasNotice);
        Assert.False(vm.HasError);
        Assert.False(vm.HasResult);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Merge_PassesACancellableToken()
    {
        _finder.AddFolder(Folder, "a.pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.True(_merger.LastToken.CanBeCanceled);
    }

    [Fact]
    public async Task Merge_AfterACancel_WorksAgainAndClearsTheNotice()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _merger.Gate = new ManualResetEventSlim(false);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        var first = vm.MergeCommand.ExecuteAsync(null);
        vm.CancelCommand.Execute(null);
        await first;

        _merger.Gate = null;
        await vm.MergeCommand.ExecuteAsync(null);

        Assert.False(vm.HasNotice);
        Assert.True(vm.HasResult);
    }

    [Fact]
    public async Task SelectingAnotherFolder_ClearsTheCancelledNotice()
    {
        _finder.AddFolder(Folder, "a.pdf");
        _finder.AddFolder(@"C:\docs\Other", "b.pdf");
        _merger.Gate = new ManualResetEventSlim(false);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(Folder);
        var running = vm.MergeCommand.ExecuteAsync(null);
        vm.CancelCommand.Execute(null);
        await running;

        vm.SelectFolderCommand.Execute(@"C:\docs\Other");

        Assert.False(vm.HasNotice);
    }
}
