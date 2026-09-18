using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

internal class Program
{
    static void Main(string[] args)
    {
        var sourceFolderPath = GetSourceFolderPath();

        if (!string.IsNullOrEmpty(sourceFolderPath))
        {
            var mergeFilePath = Path.Combine(sourceFolderPath, $"Merge_{Path.GetFileName(sourceFolderPath)}_{DateTime.Now:yyyyMMddHHmmss}.pdf");

            MergePdfFilesInFolder(sourceFolderPath, mergeFilePath);

            Console.WriteLine($"Merged File: {mergeFilePath}");
        }

        Console.WriteLine("DONE.");
    }

    private static string? GetSourceFolderPath()
    {
        Console.WriteLine("Enter the folder path:");
        var sourceFolderPath = Console.ReadLine();

        if (!Directory.Exists(sourceFolderPath))
        {
            Console.WriteLine("Invalid Path.");
            return null;
        }

        return sourceFolderPath;
    }

    private static void MergePdfFilesInFolder(string sourceFolderPath, string mergeFilePath)
    {
        var files = Directory.GetFiles(sourceFolderPath, "*.pdf");
        using PdfDocument outputPDFDocument = new();
        int pageOffset = 0;

        foreach (var pdfFile in files)
        {
            pageOffset += Merge(outputPDFDocument, pdfFile, pageOffset);
        }

        outputPDFDocument.Save(mergeFilePath);
    }

    private static int Merge(PdfDocument output, string pdfFile, int pageOffset)
    {
        using var input = PdfReader.Open(pdfFile, PdfDocumentOpenMode.Import);
        output.Version = input.Version;

        // Build page index map before pages are imported into the output document
        var pageIndexMap = BuildPageIndexMap(input);

        foreach (PdfPage page in input.Pages)
        {
            output.AddPage(page);
        }

        var fileBookmark = output.Outlines.Add(Path.GetFileNameWithoutExtension(pdfFile), output.Pages[pageOffset]);

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
