using ExoPdf.Core.Models;
using ExoPdf.Core.Operations;

namespace ExoPdf.Tests;

public class PdfMergerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly PdfMerger _merger = new();

    public PdfMergerTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private MergeResult RunMerge() => _merger.Merge(new MergeOptions { SourceFolderPath = _folder });

    [Fact]
    public void Merge_SingleFile_ReturnsCorrectPageCount()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 3);

        var result = RunMerge();

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void Merge_MultipleFiles_CombinesPageCounts()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 2);
        PdfFixture.CreatePdf(_folder, "b.pdf", 4);

        var result = RunMerge();

        Assert.Equal(6, result.TotalPages);
    }

    [Fact]
    public void Merge_MultipleFiles_ReturnsCorrectFileCount()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        PdfFixture.CreatePdf(_folder, "b.pdf", 1);
        PdfFixture.CreatePdf(_folder, "c.pdf", 1);

        var result = RunMerge();

        Assert.Equal(3, result.FilesMerged);
    }

    [Fact]
    public void Merge_OutputSavedInSourceFolder()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);

        var result = RunMerge();

        Assert.StartsWith(_folder, result.OutputFilePath);
        Assert.True(File.Exists(result.OutputFilePath));
    }

    [Fact]
    public void Merge_EachFileGetsTopLevelBookmark()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);
        PdfFixture.CreatePdf(_folder, "b.pdf", 1);

        var result = RunMerge();

        using var output = PdfFixture.OpenResult(result.OutputFilePath);
        Assert.Equal(2, output.Outlines.Count);
    }

    [Fact]
    public void Merge_BookmarkTitlesMatchFileNamesWithoutExtension()
    {
        PdfFixture.CreatePdf(_folder, "report.pdf", 1);
        PdfFixture.CreatePdf(_folder, "summary.pdf", 1);

        var result = RunMerge();

        using var output = PdfFixture.OpenResult(result.OutputFilePath);
        var titles = output.Outlines.Cast<PdfSharp.Pdf.PdfOutline>().Select(o => o.Title).ToList();
        Assert.Contains("report", titles);
        Assert.Contains("summary", titles);
    }

    [Fact]
    public void Merge_FileWithBookmarks_PreservesAsChildren()
    {
        PdfFixture.CreatePdfWithBookmarks(_folder, "a.pdf", 3, "Chapter 1", "Chapter 2");

        var result = RunMerge();

        using var output = PdfFixture.OpenResult(result.OutputFilePath);
        var fileBookmark = output.Outlines.Cast<PdfSharp.Pdf.PdfOutline>().First();
        Assert.Equal(2, fileBookmark.Outlines.Count);
    }

    [Fact]
    public void Merge_FileWithoutBookmarks_HasNoChildren()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 2);

        var result = RunMerge();

        using var output = PdfFixture.OpenResult(result.OutputFilePath);
        var fileBookmark = output.Outlines.Cast<PdfSharp.Pdf.PdfOutline>().First();
        Assert.Empty(fileBookmark.Outlines);
    }
}
