using System.IO.Abstractions;
using ExoPdf.Core.Errors;
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
            throw new NoPdfFilesException(options.SourceFolderPath);

        var outputFilePath = namer.CreateOutputPath(options.SourceFolderPath);
        var tempFilePath = outputFilePath + ".tmp";

        try
        {
            int totalPages = 0;

            using (PdfDocument output = new())
            {
                foreach (var file in files)
                    totalPages += MergeFile(output, file, totalPages);

                Write(output, tempFilePath, outputFilePath, options.SourceFolderPath);
            }

            return new MergeResult
            {
                OutputFilePath = outputFilePath,
                FilesMerged = files.Count,
                TotalPages = totalPages
            };
        }
        catch
        {
            TryDelete(tempFilePath);
            throw;
        }
    }

    /// <summary>Best-effort cleanup: failing to delete must not hide the error that got us here.</summary>
    private void TryDelete(string path)
    {
        try
        {
            if (fileSystem.File.Exists(path))
                fileSystem.File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Saves to a temporary file, then moves it into place, so only a complete file is ever published.</summary>
    private void Write(PdfDocument output, string tempFilePath, string outputFilePath, string folderPath)
    {
        try
        {
            using (var stream = fileSystem.File.Create(tempFilePath))
                output.Save(stream, closeStream: false);

            fileSystem.File.Move(tempFilePath, outputFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new OutputWriteException(folderPath, ex);
        }
    }

    private int MergeFile(PdfDocument output, string filePath, int pageOffset)
    {
        try
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
        catch (Exception ex) when (ex is not ExoPdfException and not OperationCanceledException)
        {
            // Whatever went wrong while reading this one file (corrupt, encrypted,
            // locked, gone), report it as that file being unreadable.
            throw new PdfUnreadableException(filePath, ex);
        }
    }
}
