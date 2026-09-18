using System.CommandLine;
using ExoPdf.Core.Models;
using ExoPdf.Core.Operations;

namespace ExoPdf.Cli.Commands;

public static class MergeCommand
{
    public static Command Build()
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
            var merger = new PdfMerger();
            var result = merger.Merge(new MergeOptions { SourceFolderPath = folder.FullName });

            Console.WriteLine($"Merged {result.FilesMerged} files ({result.TotalPages} pages)");
            Console.WriteLine($"Output: {result.OutputFilePath}");
        });

        return command;
    }
}
