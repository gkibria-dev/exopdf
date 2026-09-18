using System.IO.Abstractions;
using ExoPdf.Core.Errors;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ExoPdf.Core.Operations;

public sealed class PdfMerger(IFileSystem fileSystem, IMergeSourceFinder finder, MergeOutputNamer namer) : IPdfMerger
{
    public MergeResult Merge(
        MergeOptions options,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = ResolveFiles(options);
        if (files.Count == 0)
            throw new NoPdfFilesException(options.SourceFolderPath);

        var outputFilePath = namer.CreateOutputPath(options.SourceFolderPath);
        var tempFilePath = outputFilePath + ".tmp";

        try
        {
            int totalPages = 0;

            using (PdfDocument output = new())
            {
                for (int i = 0; i < files.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    totalPages += MergeFile(output, files[i], totalPages);
                    progress?.Report(new MergeProgress(i + 1, files.Count, fileSystem.Path.GetFileName(files[i])));
                }

                // The last point at which the merge can still be cancelled: saving cannot be.
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new MergeProgress(files.Count, files.Count, "", MergeStage.Saving));
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

    private IReadOnlyList<string> ResolveFiles(MergeOptions options)
    {
        if (options.Files is null)
            return finder.Find(options.SourceFolderPath);

        // The finder checks the folder when it scans it; with an explicit list the
        // folder is only the output location, so check it here.
        if (!fileSystem.Directory.Exists(options.SourceFolderPath))
            throw new SourceFolderNotFoundException(options.SourceFolderPath);

        return options.Files;
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
        using var stream = ReadingFile(filePath, () => fileSystem.File.OpenRead(filePath));
        using var input = ReadingFile(filePath, () => OpenPdf(stream));

        output.Version = input.Version;
        ReadingFile(filePath, () =>
        {
            foreach (PdfPage page in input.Pages)
                output.AddPage(page);
        });

        // From here on it is our own code working on a file that read fine; a failure
        // here is a bug and is deliberately not reported as an unreadable file.
        var fileBookmark = output.Outlines.Add(
            fileSystem.Path.GetFileNameWithoutExtension(filePath),
            output.Pages[pageOffset]);

        OutlineCopier.Copy(input, fileBookmark.Outlines, output, pageOffset);

        return input.PageCount;
    }

    private static PdfDocument OpenPdf(Stream stream)
    {
        var document = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        if (document.PageCount == 0)
        {
            document.Dispose();
            throw new InvalidOperationException("The file contains no pages.");
        }

        return document;
    }

    /// <summary>
    /// Runs a step that reads the source file. Whatever fails while reading (corrupt,
    /// encrypted, locked, gone) is reported as that file being unreadable.
    /// </summary>
    private static T ReadingFile<T>(string filePath, Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (ex is not ExoPdfException and not OperationCanceledException)
        {
            throw new PdfUnreadableException(filePath, ex);
        }
    }

    private static void ReadingFile(string filePath, Action read) =>
        ReadingFile(filePath, () =>
        {
            read();
            return 0;
        });
}
