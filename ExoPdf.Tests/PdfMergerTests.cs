using System.IO.Abstractions.TestingHelpers;
using ExoPdf.Core.Errors;
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
        Assert.Throws<SourceFolderNotFoundException>(
            () => _merger.Merge(new MergeOptions { SourceFolderPath = @"C:\docs\missing" }));
    }

    [Fact]
    public void Merge_NoPdfFiles_Throws()
    {
        var exception = Assert.Throws<NoPdfFilesException>(RunMerge);

        Assert.Equal(Folder, exception.FolderPath);
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

        Assert.ThrowsAny<ExoPdfException>(RunMerge);

        Assert.Equal(["a.pdf", "broken.pdf"], _fileSystem.Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Merge_UnreadablePdf_ReportsWhichFileIsBroken()
    {
        AddPdf("a.pdf", 1);
        var brokenPath = Path.Combine(Folder, "broken.pdf");
        _fileSystem.AddFile(brokenPath, new MockFileData("this is not a pdf"));

        var exception = Assert.Throws<PdfUnreadableException>(RunMerge);

        Assert.Equal(brokenPath, exception.FilePath);
        Assert.Contains("\"broken.pdf\"", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void Merge_PdfWithNoPages_IsReportedByNameWithTheReason()
    {
        AddPdf("a.pdf", 1);
        var emptyPath = Path.Combine(Folder, "nopages.pdf");
        _fileSystem.AddFile(emptyPath, new MockFileData(PdfFixture.ZeroPagePdfBytes()));

        var exception = Assert.Throws<PdfUnreadableException>(RunMerge);

        Assert.Equal(emptyPath, exception.FilePath);
        Assert.Contains("no pages", exception.Message);
        Assert.Equal(["a.pdf", "nopages.pdf"], _fileSystem.Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Merge_EmptyFile_IsReportedAsUnreadable()
    {
        _fileSystem.AddFile(Path.Combine(Folder, "empty.pdf"), new MockFileData(""));

        Assert.Throws<PdfUnreadableException>(RunMerge);
    }

    [Fact]
    public void Merge_OutputCannotBeWritten_ReportsTheFolderAndLeavesNothingBehind()
    {
        AddPdf("a.pdf", 1);
        // A read-only file where the temporary file will be created makes writing fail.
        _fileSystem.AddFile(
            Path.Combine(Folder, "Merge_Invoices_20260918103005.pdf.tmp"),
            new MockFileData("") { Attributes = FileAttributes.ReadOnly });

        var exception = Assert.Throws<OutputWriteException>(RunMerge);

        Assert.Equal(Folder, exception.FolderPath);
        Assert.DoesNotContain(_fileSystem.Directory.GetFiles(Folder), f => f.EndsWith(".pdf") && f.Contains("Merge_"));
    }

    [Fact]
    public void Merge_DoesNotModifySourceFiles()
    {
        AddPdf("a.pdf", 2);
        var before = _fileSystem.File.ReadAllBytes(Path.Combine(Folder, "a.pdf"));

        RunMerge();

        Assert.Equal(before, _fileSystem.File.ReadAllBytes(Path.Combine(Folder, "a.pdf")));
    }

    // --- progress -----------------------------------------------------------

    [Fact]
    public void Merge_ReportsProgressAfterEachFile()
    {
        AddPdf("a.pdf", 1);
        AddPdf("b.pdf", 1);
        AddPdf("c.pdf", 1);
        var progress = new RecordingProgress<MergeProgress>();

        _merger.Merge(new MergeOptions { SourceFolderPath = Folder }, progress);

        Assert.Equal(
            [new MergeProgress(1, 3, "a.pdf"), new MergeProgress(2, 3, "b.pdf"), new MergeProgress(3, 3, "c.pdf")],
            progress.Reports);
    }

    [Fact]
    public void Merge_WithoutAProgressSink_StillWorks()
    {
        AddPdf("a.pdf", 1);

        Assert.Equal(1, _merger.Merge(new MergeOptions { SourceFolderPath = Folder }).FilesMerged);
    }

    // --- cancellation -------------------------------------------------------

    [Fact]
    public void Merge_AlreadyCancelled_ThrowsAndLeavesNothingBehind()
    {
        AddPdf("a.pdf", 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => _merger.Merge(new MergeOptions { SourceFolderPath = Folder }, null, cts.Token));

        Assert.Equal(["a.pdf"], _fileSystem.Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Merge_CancelledBetweenFiles_StopsWithoutMergingTheRestAndLeavesNoOutput()
    {
        AddPdf("a.pdf", 1);
        AddPdf("b.pdf", 1);
        AddPdf("c.pdf", 1);
        using var cts = new CancellationTokenSource();
        var progress = new RecordingProgress<MergeProgress>(report =>
        {
            if (report.FilesCompleted == 1)
                cts.Cancel();
        });

        Assert.Throws<OperationCanceledException>(
            () => _merger.Merge(new MergeOptions { SourceFolderPath = Folder }, progress, cts.Token));

        Assert.Single(progress.Reports);
        Assert.Equal(["a.pdf", "b.pdf", "c.pdf"], _fileSystem.Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Merge_CancelledAfterTheLastFile_StillPublishesNothing()
    {
        AddPdf("a.pdf", 1);
        using var cts = new CancellationTokenSource();
        var progress = new RecordingProgress<MergeProgress>(_ => cts.Cancel());

        Assert.Throws<OperationCanceledException>(
            () => _merger.Merge(new MergeOptions { SourceFolderPath = Folder }, progress, cts.Token));

        Assert.Equal(["a.pdf"], _fileSystem.Directory.GetFiles(Folder).Select(Path.GetFileName));
    }

    // --- explicit file lists ------------------------------------------------

    private MergeResult RunMergeOf(params string[] fileNames) => _merger.Merge(new MergeOptions
    {
        SourceFolderPath = Folder,
        Files = fileNames.Select(name => Path.Combine(Folder, name)).ToList()
    });

    [Fact]
    public void Merge_ExplicitFiles_AreMergedInTheGivenOrder()
    {
        AddPdf("a.pdf", 1);
        AddPdf("b.pdf", 1);

        using var output = OpenOutput(RunMergeOf("b.pdf", "a.pdf"));

        Assert.Equal(["b", "a"], Titles(output.Outlines));
    }

    [Fact]
    public void Merge_ExplicitFiles_OnlyThoseFilesAreMerged()
    {
        AddPdf("a.pdf", 1);
        AddPdf("b.pdf", 2);
        AddPdf("c.pdf", 4);

        var result = RunMergeOf("a.pdf", "c.pdf");

        Assert.Equal(2, result.FilesMerged);
        Assert.Equal(5, result.TotalPages);
    }

    [Fact]
    public void Merge_ExplicitFiles_OutputGoesToTheSourceFolder()
    {
        AddPdf("a.pdf", 1);

        var result = RunMergeOf("a.pdf");

        Assert.Equal(Path.Combine(Folder, "Merge_Invoices_20260918103005.pdf"), result.OutputFilePath);
    }

    [Fact]
    public void Merge_ExplicitFiles_FromAnotherFolder_AreAccepted()
    {
        _fileSystem.AddFile(@"C:\other\x.pdf", new MockFileData(PdfFixture.PdfBytes(2)));

        var result = _merger.Merge(new MergeOptions { SourceFolderPath = Folder, Files = [@"C:\other\x.pdf"] });

        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public void Merge_ExplicitFiles_MissingFile_IsReportedByName()
    {
        AddPdf("a.pdf", 1);

        var exception = Assert.Throws<PdfUnreadableException>(() => RunMergeOf("a.pdf", "gone.pdf"));

        Assert.Contains("gone.pdf", exception.Message);
    }

    [Fact]
    public void Merge_ExplicitEmptyList_Throws()
    {
        AddPdf("a.pdf", 1);

        Assert.Throws<NoPdfFilesException>(() => RunMergeOf());
    }

    [Fact]
    public void Merge_ExplicitFiles_MissingOutputFolder_Throws()
    {
        _fileSystem.AddFile(@"C:\other\x.pdf", new MockFileData(PdfFixture.PdfBytes(1)));

        Assert.Throws<SourceFolderNotFoundException>(() => _merger.Merge(new MergeOptions
        {
            SourceFolderPath = @"C:\docs\missing",
            Files = [@"C:\other\x.pdf"]
        }));
    }

    [Fact]
    public void Merge_ExplicitFiles_IgnoreTheFolderScan()
    {
        AddPdf("a.pdf", 1);
        AddPdf("b.pdf", 1);

        Assert.Equal(1, RunMergeOf("a.pdf").FilesMerged);
    }
}
