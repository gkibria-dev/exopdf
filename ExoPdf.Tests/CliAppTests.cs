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
    public void Interrupt_PassesTheTokenToTheMerge()
    {
        using var cancellation = new CancellationTokenSource();

        CliApp.Run(["merge", @"C:\docs"], _merger, new InvocationConfiguration { Output = _output, Error = _error }, cancellation.Token);

        Assert.Equal(cancellation.Token, _merger.LastToken);
    }

    [Fact]
    public void Interrupt_ReturnsTheInterruptExitCodeWithAMessage_AndWritesNoOutput()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exitCode = CliApp.Run(
            ["merge", @"C:\docs"], _merger, new InvocationConfiguration { Output = _output, Error = _error }, cancellation.Token);

        Assert.Equal(CliApp.CancelledExitCode, exitCode);
        Assert.Equal("Merge cancelled.", _error.ToString().Trim());
        Assert.Equal("", _output.ToString());
    }

    [Fact]
    public void UnexpectedFailure_IsAMessageNotAStackTrace_AndReturnsTwo()
    {
        _merger.Exception = new NullReferenceException("a bug");

        var exitCode = Run("merge", @"C:\docs");

        Assert.Equal(CliApp.UnexpectedErrorExitCode, exitCode);
        Assert.Equal("Unexpected error (NullReferenceException): a bug", _error.ToString().Trim());
        Assert.Equal("", _output.ToString());
    }
}
