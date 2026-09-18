using System.CommandLine;
using ExoPdf.Core.Errors;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Models;

namespace ExoPdf.Cli.Commands;

public static class MergeCommand
{
    public static Command Build(IPdfMerger merger)
    {
        var folderArgument = new Argument<DirectoryInfo>("folder")
        {
            Description = "Folder containing the PDF files to merge"
        };

        var command = new Command("merge", "Merge all PDF files in a folder into one")
        {
            folderArgument
        };

        command.SetAction(parseResult =>
        {
            var folder = parseResult.GetValue(folderArgument)!;
            var output = parseResult.InvocationConfiguration.Output;
            var error = parseResult.InvocationConfiguration.Error;

            try
            {
                var result = merger.Merge(new MergeOptions { SourceFolderPath = folder.FullName });

                output.WriteLine($"Merged {result.FilesMerged} files ({result.TotalPages} pages)");
                output.WriteLine($"Output: {result.OutputFilePath}");
                return 0;
            }
            catch (ExoPdfException ex)
            {
                // An expected failure (missing folder, unreadable PDF, ...): show the
                // message. Anything else is a bug and is left to CliApp.
                error.WriteLine(ex.Message);
                return 1;
            }
        });

        return command;
    }
}
