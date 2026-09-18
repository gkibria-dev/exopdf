using System.IO.Abstractions.TestingHelpers;
using ExoPdf.Core.Errors;
using ExoPdf.Core.Merging;
using Microsoft.Extensions.Time.Testing;

namespace ExoPdf.Tests;

public class MergeSourceFinderTests
{
    private const string Folder = @"C:\docs\Invoices";

    private readonly MockFileSystem _fileSystem = new();
    private readonly MergeSourceFinder _finder;

    public MergeSourceFinderTests()
    {
        _fileSystem.Directory.CreateDirectory(Folder);
        var namer = new MergeOutputNamer(_fileSystem, new FakeTimeProvider());
        _finder = new MergeSourceFinder(_fileSystem, namer);
    }

    private void AddFile(string name) =>
        _fileSystem.AddFile(Path.Combine(Folder, name), new MockFileData("x"));

    private IEnumerable<string?> FoundNames(string folder = Folder) =>
        _finder.Find(folder).Select(Path.GetFileName);

    [Fact]
    public void Find_ReturnsFilesInFileNameOrder()
    {
        AddFile("c.pdf");
        AddFile("a.pdf");
        AddFile("b.pdf");

        Assert.Equal(["a.pdf", "b.pdf", "c.pdf"], FoundNames());
    }

    [Fact]
    public void Find_OrdersCaseInsensitively()
    {
        AddFile("b.pdf");
        AddFile("A.pdf");
        AddFile("C.pdf");

        Assert.Equal(["A.pdf", "b.pdf", "C.pdf"], FoundNames());
    }

    [Fact]
    public void Find_IgnoresNonPdfFilesAndSubfolders()
    {
        AddFile("a.pdf");
        AddFile("notes.txt");
        _fileSystem.AddFile(Path.Combine(Folder, "sub", "inner.pdf"), new MockFileData("x"));

        Assert.Equal(["a.pdf"], FoundNames());
    }

    [Fact]
    public void Find_ExcludesPreviousMergeOutput()
    {
        AddFile("a.pdf");
        AddFile("Merge_Invoices_20260101120000.pdf");

        Assert.Equal(["a.pdf"], FoundNames());
    }

    [Fact]
    public void Find_KeepsSimilarlyNamedUserFiles()
    {
        AddFile("Merge_Invoices_notes.pdf");
        AddFile("Merge_OtherFolder_20260101120000.pdf");

        Assert.Equal(2, _finder.Find(Folder).Count);
    }

    [Fact]
    public void Find_FolderPathWithTrailingSeparator_StillExcludesPreviousOutput()
    {
        AddFile("a.pdf");
        AddFile("Merge_Invoices_20260101120000.pdf");

        Assert.Equal(["a.pdf"], FoundNames(Folder + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Find_EmptyFolder_ReturnsNothing()
    {
        Assert.Empty(_finder.Find(Folder));
    }

    [Fact]
    public void Find_MissingFolder_Throws()
    {
        var exception = Assert.Throws<SourceFolderNotFoundException>(() => _finder.Find(@"C:\docs\missing"));

        Assert.Equal(@"C:\docs\missing", exception.FolderPath);
    }
}
