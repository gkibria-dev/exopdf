using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ExoPdf.Tests;

internal static class PdfFixture
{
    internal static string CreatePdf(string folder, string fileName, int pageCount)
    {
        using PdfDocument doc = new();
        for (int i = 0; i < pageCount; i++)
            doc.AddPage();

        var path = Path.Combine(folder, fileName);
        doc.Save(path);
        return path;
    }

    internal static string CreatePdfWithBookmarks(string folder, string fileName, int pageCount, params string[] bookmarkTitles)
    {
        using PdfDocument doc = new();
        for (int i = 0; i < pageCount; i++)
            doc.AddPage();

        foreach (var title in bookmarkTitles)
        {
            int pageIndex = Math.Min(bookmarkTitles.ToList().IndexOf(title), doc.PageCount - 1);
            doc.Outlines.Add(title, doc.Pages[pageIndex]);
        }

        var path = Path.Combine(folder, fileName);
        doc.Save(path);
        return path;
    }

    internal static PdfDocument OpenResult(string filePath) =>
        PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
}
