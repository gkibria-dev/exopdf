using System.Text.RegularExpressions;
using ExoPdf.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ExoPdf.Core.Operations;

public class PdfMerger
{
    /// <summary>
    /// Returns the PDF files that <see cref="Merge"/> would combine, in merge order:
    /// ascending file name, case-insensitive. Output files from previous merges of
    /// the same folder are excluded.
    /// </summary>
    public IReadOnlyList<string> GetSourceFiles(string folderPath)
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

        var previousOutput = new Regex(
            $"^Merge_{Regex.Escape(GetFolderName(folderPath))}_\\d{{14}}\\.pdf$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return Directory.GetFiles(folderPath, "*.pdf")
            .Where(file => !previousOutput.IsMatch(Path.GetFileName(file)))
            .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public MergeResult Merge(MergeOptions options)
    {
        var files = GetSourceFiles(options.SourceFolderPath);
        if (files.Count == 0)
            throw new InvalidOperationException($"No PDF files to merge in: {options.SourceFolderPath}");

        var outputFilePath = Path.Combine(
            options.SourceFolderPath,
            $"Merge_{GetFolderName(options.SourceFolderPath)}_{DateTime.Now:yyyyMMddHHmmss}.pdf");

        using PdfDocument output = new();
        int pageOffset = 0;

        foreach (var file in files)
            pageOffset += MergeFile(output, file, pageOffset);

        output.Save(outputFilePath);

        return new MergeResult
        {
            OutputFilePath = outputFilePath,
            FilesMerged = files.Count,
            TotalPages = pageOffset
        };
    }

    private static string GetFolderName(string folderPath) =>
        Path.GetFileName(Path.TrimEndingDirectorySeparator(folderPath));

    private static int MergeFile(PdfDocument output, string filePath, int pageOffset)
    {
        using var input = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
        output.Version = input.Version;

        var pageIndexMap = BuildPageIndexMap(input);

        foreach (PdfPage page in input.Pages)
            output.AddPage(page);

        var fileBookmark = output.Outlines.Add(
            Path.GetFileNameWithoutExtension(filePath),
            output.Pages[pageOffset]);

        if (input.Outlines.Count > 0)
            CopyOutlines(input.Outlines, fileBookmark.Outlines, output, pageIndexMap, pageOffset);

        return input.PageCount;
    }

    private static Dictionary<PdfPage, int> BuildPageIndexMap(PdfDocument input)
    {
        var map = new Dictionary<PdfPage, int>();
        for (int i = 0; i < input.PageCount; i++)
            map[input.Pages[i]] = i;
        return map;
    }

    private static void CopyOutlines(
        PdfOutlineCollection source,
        PdfOutlineCollection target,
        PdfDocument output,
        Dictionary<PdfPage, int> pageIndexMap,
        int pageOffset)
    {
        foreach (PdfOutline outline in source)
        {
            if (outline.DestinationPage == null || !pageIndexMap.TryGetValue(outline.DestinationPage, out int inputPageIndex))
                continue;

            int outputPageIndex = pageOffset + inputPageIndex;
            if (outputPageIndex >= output.PageCount)
                continue;

            var child = target.Add(outline.Title, output.Pages[outputPageIndex]);
            child.PageDestinationType = outline.PageDestinationType;

            if (outline.Outlines.Count > 0)
                CopyOutlines(outline.Outlines, child.Outlines, output, pageIndexMap, pageOffset);
        }
    }
}
