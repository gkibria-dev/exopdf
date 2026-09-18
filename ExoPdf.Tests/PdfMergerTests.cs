using System.IO.Abstractions.TestingHelpers;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Models;
using ExoPdf.Core.Operations;
using Microsoft.Extensions.Time.Testing;
using PdfSharp.Pdf;

namespace ExoPdf.Tests;

public class PdfMergerTests
{
    private const string Folder = @"C:\docs\Invoices";

    private readonly MockFileSystem _fileSystem = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 18, 10, 30, 5, TimeSpan.Zero));
    private readonly PdfMerger _merger;

    public PdfMergerTests()
    {
        _fileSystem.Directory.CreateDirectory(Folder);
        var namer = new MergeOutputNamer(_fileSystem, _time);
        _merger = new PdfMerger(_fileSystem, new MergeSourceFinder(_fileSystem, namer), namer);
    }

    private void AddPdf(string name, int pages, params string[] bookmarks) =>
        _fileSystem.AddFile(Path.Combine(Folder, name), new MockFileData(PdfFixture.PdfBytes(pages, bookmarks)));

    private MergeResult RunMerge() => _merger.Merge(new MergeOptions { SourceFolderPath = Folder });

    private PdfDocument OpenOutput(MergeResult result) => PdfFixture.OpenResult(_fileSystem, result.OutputFilePath);

    private static List<string> Titles(PdfOutlineCollection outlines) =>
        outlines.Cast<PdfOutline>().Select(o => o.Title).ToList();

    [Fact]
    public void Merge_SingleFile_ReturnsCorrectPageCount()
    {
        AddPdf("a.pdf", 3);

        Assert.Equal(3, RunMerge().TotalPages);
    }

    [Fact]
    public void Merge_MultipleFiles_CombinesPageCounts()
    {
        AddPdf("a.pdf", 2);
        AddPdf("b.pdf", 4);

        Assert.Equal(6, RunMerge().TotalPages);
    }

    [Fact]
    public void Merge_MultipleFiles_ReturnsCorrectFileCount()
    {
        AddPdf("a.pdf", 1);
        AddPdf("b.pdf", 1);
        AddPdf("c.pdf", 1);

        Assert.Equal(3, RunMerge().FilesMerged);
    }

    [Fact]
    public void Merge_OutputPageCountMatchesReportedTotal()
    {
        AddPdf("a.pdf", 2);
        AddPdf("b.pdf", 3);

        var result = RunMerge();

        using var output = OpenOutput(result);
        Assert.Equal(5, output.PageCount);
    }

    [Fact]
    public void Merge_OutputSavedInSourceFolderWithTimestampedName()
    {
        AddPdf("a.pdf", 1);

        var result = RunMerge();

        Assert.Equal(Path.Combine(Folder, "Merge_Invoices_20260918103005.pdf"), result.OutputFilePath);
        Assert.True(_fileSystem.File.Exists(result.OutputFilePath));
    }

    [Fact]
    public void Merge_SuccessLeavesNoTemporaryFile()
    {
        AddPdf("a.pdf", 1);

        RunMerge();

        Assert.DoesNotContain(_fileSystem.Directory.GetFiles(Folder), f => f.EndsWith(".tmp"));
    }

    [Fact]
    public void Merge_EachFileGetsTopLevelBookmark()
    {
        AddPdf("a.pdf", 1);
        AddPdf("b.pdf", 1);

        using var output = OpenOutput(RunMerge());

        Assert.Equal(2, output.Outlines.Count);
    }

    [Fact]
    public void Merge_BookmarkTitlesMatchFileNamesWithoutExtension()
    {
        AddPdf("report.pdf", 1);
        AddPdf("summary.pdf", 1);

        using var output = OpenOutput(RunMerge());

        Assert.Equal(["report", "summary"], Titles(output.Outlines));
    }

    [Fact]
    public void Merge_FilesAreCombinedInFileNameOrder()
    {
        AddPdf("b.pdf", 1);
        AddPdf("a.pdf", 1);

        using var output = OpenOutput(RunMerge());

        Assert.Equal(["a", "b"], Titles(output.Outlines));
    }

    [Fact]
    public void Merge_FileWithBookmarks_PreservesAsChildren()
    {
        AddPdf("a.pdf", 3, "Chapter 1", "Chapter 2");

        using var output = OpenOutput(RunMerge());

        var fileBookmark = output.Outlines.Cast<PdfOutline>().First();
        Assert.Equal(["Chapter 1", "Chapter 2"], Titles(fileBookmark.Outlines));
    }

    [Fact]
    public void Merge_ChildBookmarksPointAtPagesOffsetByPreviousFiles()
    {
        AddPdf("a.pdf", 2);
        AddPdf("b.pdf", 3, "Start", "Middle");

        using var output = OpenOutput(RunMerge());

        var fileBookmark = output.Outlines.Cast<PdfOutline>().Last();
        var children = fileBookmark.Outlines.Cast<PdfOutline>().ToList();
        Assert.Same(output.Pages[2], children[0].DestinationPage);
        Assert.Same(output.Pages[3], children[1].DestinationPage);
    }

    [Fact]
    public void Merge_FileWithoutBookmarks_HasNoChildren()
    {
        AddPdf("a.pdf", 2);

        using var output = OpenOutput(RunMerge());

        Assert.Empty(output.Outlines.Cast<PdfOutline>().First().Outlines);
    }

    [Fact]
    public void Merge_MissingFolder_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => _merger.Merge(new MergeOptions { SourceFolderPath = @"C:\docs\missing" }));
    }

    [Fact]
    public void Merge_NoPdfFiles_Throws()
    {
        Assert.Throws<InvalidOperationException>(RunMerge);
    }

    [Fact]
    public void Merge_MergingTwice_DoesNotIncludePreviousOutput()
    {
        AddPdf("a.pdf", 2);
        AddPdf("b.pdf", 3);
        RunMerge();

        var second = RunMerge();

        Assert.Equal(2, second.FilesMerged);
        Assert.Equal(5, second.TotalPages);
    }

    [Fact]
    public void Merge_TwiceInTheSameSecond_KeepsBothOutputs()
    {
        AddPdf("a.pdf", 1);

        var first = RunMerge();
        var second = RunMerge();

        Assert.NotEqual(first.OutputFilePath, second.OutputFilePath);
        Assert.True(_fileSystem.File.Exists(first.OutputFilePath));
        Assert.True(_fileSystem.File.Exists(second.OutputFilePath));
    }

    [Fact]
    public void Merge_UnreadablePdf_LeavesNoOutputAndNoTemporaryFile()
    {
        AddPdf("a.pdf", 1);
        _fileSystem.AddFile(Path.Combine(Folder, "broken.pdf"), new MockFileData("this is not a pdf"));

        Assert.ThrowsAny<Exception>(RunMerge);

        Assert.Equal(["a.pdf", "broken.pdf"], _fileSystem.Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Merge_DoesNotModifySourceFiles()
    {
        AddPdf("a.pdf", 2);
        var before = _fileSystem.File.ReadAllBytes(Path.Combine(Folder, "a.pdf"));

        RunMerge();

        Assert.Equal(before, _fileSystem.File.ReadAllBytes(Path.Combine(Folder, "a.pdf")));
    }
}
