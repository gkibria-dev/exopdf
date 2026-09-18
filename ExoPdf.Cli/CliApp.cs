using System.CommandLine;
using ExoPdf.Cli.Commands;
using ExoPdf.Core.Merging;

namespace ExoPdf.Cli;

public static class CliApp
{
    /// <summary>Exit code for an unexpected failure. Expected failures use 1.</summary>
    public const int UnexpectedErrorExitCode = 2;

    public static int Run(string[] args, IPdfMerger merger, InvocationConfiguration? configuration = null)
    {
        configuration ??= new InvocationConfiguration();

        // System.CommandLine would otherwise print the exception itself and return 1,
        // which is indistinguishable from an expected failure.
        configuration.EnableDefaultExceptionHandler = false;

        var rootCommand = new RootCommand("ExoPdf — PDF manipulation utility");
        rootCommand.Add(MergeCommand.Build(merger));

        try
        {
            return rootCommand.Parse(args).Invoke(configuration);
        }
        catch (Exception ex)
        {
            // Last resort: a bug should read as a message, not a stack trace.
            configuration.Error.WriteLine($"Unexpected error: {ex.Message}");
            return UnexpectedErrorExitCode;
        }
    }
}
