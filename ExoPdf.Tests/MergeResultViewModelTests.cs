using ExoPdf.Core.Models;
using ExoPdf.Desktop.Services;
using ExoPdf.Desktop.ViewModels;

namespace ExoPdf.Tests;

public class MergeResultViewModelTests
{
    private const string Output = @"C:\docs\Invoices\Merge_Invoices_20260918103005.pdf";

    private readonly FakeShellLauncher _shell = new();

    private MergeResultViewModel Create(int files = 3, int pages = 6) =>
        new(new MergeResult { OutputFilePath = Output, FilesMerged = files, TotalPages = pages }, _shell);

    [Fact]
    public void Summary_UsesPluralWords()
    {
        Assert.Equal("Merged 3 files (6 pages)", Create().Summary);
    }

    [Fact]
    public void Summary_UsesSingularWords()
    {
        Assert.Equal("Merged 1 file (1 page)", Create(files: 1, pages: 1).Summary);
    }

    [Fact]
    public void OutputFilePath_IsTheMergedFile()
    {
        Assert.Equal(Output, Create().OutputFilePath);
    }

    [Fact]
    public void Open_LaunchesTheOutputFile()
    {
        Create().OpenCommand.Execute(null);

        Assert.Equal([Output], _shell.OpenedFiles);
    }

    [Fact]
    public void ShowInFolder_SelectsTheOutputFile()
    {
        Create().ShowInFolderCommand.Execute(null);

        Assert.Equal([Output], _shell.ShownInFolder);
    }

    [Fact]
    public void Open_LauncherFails_ShowsTheMessageOnTheResult()
    {
        _shell.ThrowOnLaunch = new ShellLaunchException("no viewer");
        var vm = Create();

        vm.OpenCommand.Execute(null);

        Assert.Equal("no viewer", vm.LaunchError);
        Assert.True(vm.HasLaunchError);
    }

    [Fact]
    public void Open_AfterAFailure_ClearsTheOldMessageWhenItWorks()
    {
        _shell.ThrowOnLaunch = new ShellLaunchException("no viewer");
        var vm = Create();
        vm.OpenCommand.Execute(null);

        _shell.ThrowOnLaunch = null;
        vm.OpenCommand.Execute(null);

        Assert.False(vm.HasLaunchError);
    }

    [Fact]
    public void NewResult_HasNoLaunchError()
    {
        Assert.False(Create().HasLaunchError);
    }
}
