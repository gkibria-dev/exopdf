using System.CommandLine;
using ExoPdf.Cli.Commands;
using ExoPdf.Core.Merging;

namespace ExoPdf.Cli;

public static class CliApp
{
    /// <summary>Exit code for an unexpected failure. Expected failures use 1.</summary>
    public const int UnexpectedErrorExitCode = 2;

    /// <summary>Exit code when the user interrupts (Ctrl+C), by shell convention 128 + SIGINT.</summary>
    public const int CancelledExitCode = 130;

    public static int Run(
        string[] args,
        IPdfMerger merger,
        InvocationConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        configuration ??= new InvocationConfiguration();

        // System.CommandLine would otherwise print the exception itself and return 1,
        // which is indistinguishable from an expected failure.
        configuration.EnableDefaultExceptionHandler = false;

        var rootCommand = new RootCommand("ExoPdf — PDF manipulation utility");
        rootCommand.Add(MergeCommand.Build(merger, cancellationToken));

        try
        {
            return rootCommand.Parse(args).Invoke(configuration);
        }
        catch (Exception ex)
        {
            // Last resort: a bug should read as a message, not a stack trace. The type
            // name is included so the message is useful in a bug report.
            configuration.Error.WriteLine($"Unexpected error ({ex.GetType().Name}): {ex.Message}");
            return UnexpectedErrorExitCode;
        }
    }
}
