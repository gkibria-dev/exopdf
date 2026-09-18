using System.CommandLine;
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
            try
            {
                var result = merger.Merge(new MergeOptions { SourceFolderPath = folder.FullName });

                Console.WriteLine($"Merged {result.FilesMerged} files ({result.TotalPages} pages)");
                Console.WriteLine($"Output: {result.OutputFilePath}");
                return 0;
            }
            catch (Exception ex)
            {
                // Same policy as the Desktop frontend: any failure (missing folder,
                // corrupt or locked PDF, no write access) is a message and exit code 1.
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        });

        return command;
    }
}
