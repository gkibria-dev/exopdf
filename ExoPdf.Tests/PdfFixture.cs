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

    /// <summary>A structurally valid PDF whose page tree is empty. PDFsharp cannot save one, so it is written by hand.</summary>
    internal static byte[] ZeroPagePdfBytes()
    {
        var objects = new[]
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [] /Count 0 >>\nendobj\n"
        };

        var text = new System.Text.StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        foreach (var obj in objects)
        {
            offsets.Add(text.Length);
            text.Append(obj);
        }

        var xrefOffset = text.Length;
        text.Append("xref\n0 3\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            text.Append($"{offset:D10} 00000 n \n");
        text.Append($"trailer\n<< /Size 3 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return System.Text.Encoding.ASCII.GetBytes(text.ToString());
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
