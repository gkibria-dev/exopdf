using ExoPdf.Core.Operations;
using ExoPdf.Desktop.ViewModels;

namespace ExoPdf.Tests;

public class MergeViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly FakeFolderPicker _picker = new();
    private readonly FakeShellLauncher _shell = new();
    private readonly FakeSettingsStore _settings = new();

    public MergeViewModelTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private MergeViewModel CreateViewModel() => new(new PdfMerger(), _picker, _shell, _settings);

    [Fact]
    public void NewViewModel_HasNoFolderAndCannotMerge()
    {
        var vm = CreateViewModel();

        Assert.False(vm.HasFolder);
        Assert.False(vm.HasFiles);
        Assert.False(vm.MergeCommand.CanExecute(null));
    }

    [Fact]
    public void SelectFolder_ListsPdfsInFileNameOrder()
    {
        PdfFixture.CreatePdf(_folder, "b.pdf", 1);
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(_folder);

        Assert.Equal(["a.pdf", "b.pdf"], vm.Files.Select(f => f.Name));
        Assert.Equal("2 files", vm.FileCountText);
        Assert.True(vm.MergeCommand.CanExecute(null));
    }

    [Fact]
    public void SelectFolder_WithNoPdfs_ShowsEmptyStateAndCannotMerge()
    {
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(_folder);

        Assert.False(vm.HasFiles);
        Assert.Equal("No PDF files were found in this folder.", vm.EmptyStateText);
        Assert.False(vm.MergeCommand.CanExecute(null));
    }

    [Fact]
    public void SelectFolder_Missing_ShowsErrorAndNoFiles()
    {
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(Path.Combine(_folder, "missing"));

        Assert.True(vm.HasError);
        Assert.Empty(vm.Files);
        Assert.Equal("The PDF files in this folder could not be listed.", vm.EmptyStateText);
        Assert.False(vm.MergeCommand.CanExecute(null));
    }

    [Fact]
    public void SelectFolder_SameFolderAgain_RefreshesList()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);

        PdfFixture.CreatePdf(_folder, "b.pdf", 1);
        vm.SelectFolderCommand.Execute(_folder);

        Assert.Equal(2, vm.Files.Count);
    }

    [Fact]
    public void SelectFolder_RemembersFolderInSettings()
    {
        var vm = CreateViewModel();

        vm.SelectFolderCommand.Execute(_folder);

        Assert.Equal(_folder, _settings.Current.LastMergeFolder);
        Assert.Equal(1, _settings.SaveCount);
    }

    [Fact]
    public void NewViewModel_RestoresLastFolderIfItStillExists()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        _settings.Current.LastMergeFolder = _folder;

        var vm = CreateViewModel();

        Assert.Equal(_folder, vm.SourceFolder);
        Assert.Single(vm.Files);
    }

    [Fact]
    public void NewViewModel_IgnoresLastFolderThatNoLongerExists()
    {
        _settings.Current.LastMergeFolder = Path.Combine(_folder, "gone");

        var vm = CreateViewModel();

        Assert.False(vm.HasFolder);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void Browse_PickedFolder_IsSelected()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        _picker.Result = _folder;
        var vm = CreateViewModel();

        vm.BrowseCommand.Execute(null);

        Assert.Equal(_folder, vm.SourceFolder);
        Assert.Single(vm.Files);
    }

    [Fact]
    public void Browse_Cancelled_KeepsCurrentFolder()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);
        _picker.Result = null;

        vm.BrowseCommand.Execute(null);

        Assert.Equal(_folder, vm.SourceFolder);
        Assert.Equal(_folder, _picker.LastInitialFolder);
    }

    [Fact]
    public async Task Merge_Success_SetsResultAndClearsBusy()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 2);
        PdfFixture.CreatePdf(_folder, "b.pdf", 3);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.NotNull(vm.Result);
        Assert.True(File.Exists(vm.Result.OutputFilePath));
        Assert.Equal("Merged 2 files (5 pages)", vm.ResultSummary);
        Assert.True(vm.HasResult);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Merge_WhileRunning_IsBusyAndDisablesCommands()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);

        var running = vm.MergeCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.MergeCommand.CanExecute(null));
        Assert.False(vm.BrowseCommand.CanExecute(null));

        await running;

        Assert.False(vm.IsBusy);
        Assert.True(vm.BrowseCommand.CanExecute(null));
    }

    [Fact]
    public async Task Merge_UnreadablePdf_ShowsErrorInsteadOfThrowing()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        File.WriteAllText(Path.Combine(_folder, "broken.pdf"), "this is not a pdf");
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);

        await vm.MergeCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Null(vm.Result);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Merge_TwiceInSameFolder_SecondMergeIgnoresFirstOutput()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);

        await vm.MergeCommand.ExecuteAsync(null);
        vm.SelectFolderCommand.Execute(_folder);
        await vm.MergeCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.Result!.FilesMerged);
    }

    [Fact]
    public async Task SelectingAnotherFolder_ClearsPreviousResult()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);
        await vm.MergeCommand.ExecuteAsync(null);

        var other = Path.Combine(_folder, "other");
        Directory.CreateDirectory(other);
        vm.SelectFolderCommand.Execute(other);

        Assert.Null(vm.Result);
        Assert.False(vm.HasResult);
    }

    [Fact]
    public async Task OpenResult_LaunchesOutputFile()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);
        await vm.MergeCommand.ExecuteAsync(null);

        vm.OpenResultCommand.Execute(null);
        vm.ShowResultInFolderCommand.Execute(null);

        Assert.Equal([vm.Result!.OutputFilePath], _shell.OpenedFiles);
        Assert.Equal([vm.Result.OutputFilePath], _shell.ShownInFolder);
    }

    [Fact]
    public async Task OpenResult_LauncherFails_ShowsError()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        var vm = CreateViewModel();
        vm.SelectFolderCommand.Execute(_folder);
        await vm.MergeCommand.ExecuteAsync(null);
        _shell.ThrowOnLaunch = new InvalidOperationException("no viewer");

        vm.OpenResultCommand.Execute(null);

        Assert.Equal("no viewer", vm.ErrorMessage);
    }
}
