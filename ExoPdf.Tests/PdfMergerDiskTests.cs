using System.IO.Abstractions;
using ExoPdf.Core.Merging;
using ExoPdf.Core.Models;
using ExoPdf.Core.Operations;

namespace ExoPdf.Tests;

/// <summary>
/// A few end-to-end checks against the real disk. Everything else about merging is
/// covered in memory by <see cref="PdfMergerTests"/>.
/// </summary>
public class PdfMergerDiskTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly PdfMerger _merger;

    public PdfMergerDiskTests()
    {
        Directory.CreateDirectory(_folder);
        var fileSystem = new FileSystem();
        var namer = new MergeOutputNamer(fileSystem, TimeProvider.System);
        _merger = new PdfMerger(fileSystem, new MergeSourceFinder(fileSystem, namer), namer);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Merge_WritesReadablePdfWithAllPages()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 2);
        PdfFixture.CreatePdf(_folder, "b.pdf", 3);

        var result = _merger.Merge(new MergeOptions { SourceFolderPath = _folder });

        Assert.StartsWith(_folder, result.OutputFilePath);
        using var output = PdfFixture.OpenResult(result.OutputFilePath);
        Assert.Equal(5, output.PageCount);
    }

    [Fact]
    public void Merge_TwiceInTheSameFolder_SecondIgnoresFirstOutputAndKeepsIt()
    {
        PdfFixture.CreatePdf(_folder, "a.pdf", 1);

        var first = _merger.Merge(new MergeOptions { SourceFolderPath = _folder });
        var second = _merger.Merge(new MergeOptions { SourceFolderPath = _folder });

        Assert.Equal(1, second.FilesMerged);
        Assert.NotEqual(first.OutputFilePath, second.OutputFilePath);
        Assert.True(File.Exists(first.OutputFilePath));
    }
}
