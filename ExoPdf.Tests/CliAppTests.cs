using System.CommandLine;
using ExoPdf.Cli;
using ExoPdf.Core.Errors;

namespace ExoPdf.Tests;

public class CliAppTests
{
    private readonly FakePdfMerger _merger = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private int Run(params string[] args) =>
        CliApp.Run(args, _merger, new InvocationConfiguration { Output = _output, Error = _error });

    [Fact]
    public void Success_ReturnsZero()
    {
        Assert.Equal(0, Run("merge", @"C:\docs"));
    }

    [Fact]
    public void ExpectedFailure_ReturnsOneWithTheMessage()
    {
        _merger.Exception = new SourceFolderNotFoundException(@"C:\docs");

        var exitCode = Run("merge", @"C:\docs");

        Assert.Equal(1, exitCode);
        Assert.Contains(@"Folder not found: C:\docs", _error.ToString());
    }

    [Fact]
    public void UnexpectedFailure_IsAMessageNotAStackTrace_AndReturnsTwo()
    {
        _merger.Exception = new NullReferenceException("a bug");

        var exitCode = Run("merge", @"C:\docs");

        Assert.Equal(CliApp.UnexpectedErrorExitCode, exitCode);
        Assert.Equal("Unexpected error: a bug", _error.ToString().Trim());
        Assert.Equal("", _output.ToString());
    }
}
