using System.CommandLine;
using ExoPdf.Cli.Commands;
using ExoPdf.Core.Models;

namespace ExoPdf.Tests;

public class MergeCommandTests
{
    private readonly FakePdfMerger _merger = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private int Run(params string[] args)
    {
        var root = new RootCommand { MergeCommand.Build(_merger) };
        return root.Parse(args).Invoke(new InvocationConfiguration { Output = _output, Error = _error });
    }

    [Fact]
    public void Merge_Success_PrintsSummaryAndOutputPath_AndReturnsZero()
    {
        _merger.Result = new MergeResult { OutputFilePath = @"C:\docs\out.pdf", FilesMerged = 3, TotalPages = 6 };

        var exitCode = Run("merge", @"C:\docs");

        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["Merged 3 files (6 pages)", @"Output: C:\docs\out.pdf"],
            _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal("", _error.ToString());
    }

    [Fact]
    public void Merge_PassesTheFolderAsAFullPath()
    {
        Run("merge", ".");

        Assert.Equal(Path.GetFullPath("."), Assert.Single(_merger.Calls).SourceFolderPath);
    }

    [Fact]
    public void Merge_Failure_PrintsTheMessageToTheErrorStream_AndReturnsOne()
    {
        _merger.Exception = new InvalidOperationException("No PDF files to merge in: C:\\docs");

        var exitCode = Run("merge", @"C:\docs");

        Assert.Equal(1, exitCode);
        Assert.Contains("No PDF files to merge", _error.ToString());
        Assert.Equal("", _output.ToString());
    }

    [Fact]
    public void Merge_WithoutFolder_IsAUsageErrorAndDoesNotMerge()
    {
        var exitCode = Run("merge");

        Assert.NotEqual(0, exitCode);
        Assert.Empty(_merger.Calls);
    }
}
