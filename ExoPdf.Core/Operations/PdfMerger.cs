using System.IO.Abstractions;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ExoPdf.Core.Operations;

public sealed class PdfMerger(IFileSystem fileSystem, IMergeSourceFinder finder, MergeOutputNamer namer) : IPdfMerger
{
    public MergeResult Merge(MergeOptions options)
    {
        var files = finder.Find(options.SourceFolderPath);
        if (files.Count == 0)
            throw new InvalidOperationException($"No PDF files to merge in: {options.SourceFolderPath}");

        var outputFilePath = namer.CreateOutputPath(options.SourceFolderPath);
        var tempFilePath = outputFilePath + ".tmp";

        try
        {
            int totalPages = 0;

            using (PdfDocument output = new())
            {
                foreach (var file in files)
                    totalPages += MergeFile(output, file, totalPages);

                using var stream = fileSystem.File.Create(tempFilePath);
                output.Save(stream, closeStream: false);
            }

            // Publish only a complete file: a failure above leaves no output behind.
            fileSystem.File.Move(tempFilePath, outputFilePath);

            return new MergeResult
            {
                OutputFilePath = outputFilePath,
                FilesMerged = files.Count,
                TotalPages = totalPages
            };
        }
        catch
        {
            if (fileSystem.File.Exists(tempFilePath))
                fileSystem.File.Delete(tempFilePath);
            throw;
        }
    }

    private int MergeFile(PdfDocument output, string filePath, int pageOffset)
    {
        using var stream = fileSystem.File.OpenRead(filePath);
        using var input = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        output.Version = input.Version;

        foreach (PdfPage page in input.Pages)
            output.AddPage(page);

        var fileBookmark = output.Outlines.Add(
            fileSystem.Path.GetFileNameWithoutExtension(filePath),
            output.Pages[pageOffset]);

        OutlineCopier.Copy(input, fileBookmark.Outlines, output, pageOffset);

        return input.PageCount;
    }
}
