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

        foreach (var pdfFile in files)
        {
            Merge(outputPDFDocument, pdfFile);
        }

        outputPDFDocument.Save(mergeFilePath);
    }

    private static void Merge(PdfDocument outputPDFDocument, string pdfFile)
    {
        using var inputPDFDocument = PdfReader.Open(pdfFile, PdfDocumentOpenMode.Import);
        outputPDFDocument.Version = inputPDFDocument.Version;

        foreach (PdfPage page in inputPDFDocument.Pages)
        {
            outputPDFDocument.AddPage(page);
        }
    }
}
