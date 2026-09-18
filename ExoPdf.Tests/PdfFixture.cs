using System.IO.Abstractions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ExoPdf.Tests;

internal static class PdfFixture
{
    /// <summary>Builds a PDF in memory. Bookmark i points at page i (clamped to the last page).</summary>
    internal static byte[] PdfBytes(int pageCount, params string[] bookmarkTitles)
    {
        using PdfDocument doc = new();
        for (int i = 0; i < pageCount; i++)
            doc.AddPage();

        for (int i = 0; i < bookmarkTitles.Length; i++)
            doc.Outlines.Add(bookmarkTitles[i], doc.Pages[Math.Min(i, doc.PageCount - 1)]);

        using var stream = new MemoryStream();
        doc.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    /// <summary>Writes a PDF to the real disk.</summary>
    internal static string CreatePdf(string folder, string fileName, int pageCount, params string[] bookmarkTitles)
    {
        var path = Path.Combine(folder, fileName);
        File.WriteAllBytes(path, PdfBytes(pageCount, bookmarkTitles));
        return path;
    }

    internal static PdfDocument OpenResult(IFileSystem fileSystem, string filePath)
    {
        using var stream = fileSystem.File.OpenRead(filePath);
        return PdfReader.Open(stream, PdfDocumentOpenMode.Import);
    }

    internal static PdfDocument OpenResult(string filePath) => OpenResult(new System.IO.Abstractions.FileSystem(), filePath);
}
