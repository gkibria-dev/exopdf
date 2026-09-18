using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using ExoPdf.Core.Merging;
using Microsoft.Extensions.Time.Testing;

namespace ExoPdf.Tests;

public class MergeOutputNamerTests
{
    private const string Folder = @"C:\docs\Invoices";

    private readonly MockFileSystem _fileSystem = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 18, 10, 30, 5, TimeSpan.Zero));
    private readonly MergeOutputNamer _namer;

    public MergeOutputNamerTests()
    {
        _fileSystem.Directory.CreateDirectory(Folder);
        _namer = new MergeOutputNamer(_fileSystem, _time);
    }

    [Fact]
    public void CreateOutputPath_UsesFolderNameAndCurrentTime()
    {
        var path = _namer.CreateOutputPath(Folder);

        Assert.Equal(Path.Combine(Folder, "Merge_Invoices_20260918103005.pdf"), path);
    }

    [Fact]
    public void CreateOutputPath_FolderPathWithTrailingSeparator_UsesFolderName()
    {
        var path = _namer.CreateOutputPath(Folder + Path.DirectorySeparatorChar);

        Assert.EndsWith("Merge_Invoices_20260918103005.pdf", path);
    }

    [Fact]
    public void CreateOutputPath_NameTaken_AdvancesOneSecond()
    {
        _fileSystem.AddFile(Path.Combine(Folder, "Merge_Invoices_20260918103005.pdf"), new MockFileData(""));

        var path = _namer.CreateOutputPath(Folder);

        Assert.Equal(Path.Combine(Folder, "Merge_Invoices_20260918103006.pdf"), path);
    }

    [Fact]
    public void CreateOutputPath_SeveralNamesTaken_SkipsAllOfThem()
    {
        foreach (var second in new[] { "05", "06", "07" })
            _fileSystem.AddFile(Path.Combine(Folder, $"Merge_Invoices_202609181030{second}.pdf"), new MockFileData(""));

        var path = _namer.CreateOutputPath(Folder);

        Assert.Equal(Path.Combine(Folder, "Merge_Invoices_20260918103008.pdf"), path);
    }

    [Fact]
    public void CreateOutputPath_TimestampCarriesOverMinuteBoundary()
    {
        _time.SetUtcNow(new DateTimeOffset(2026, 9, 18, 10, 30, 59, TimeSpan.Zero));
        _fileSystem.AddFile(Path.Combine(Folder, "Merge_Invoices_20260918103059.pdf"), new MockFileData(""));

        var path = _namer.CreateOutputPath(Folder);

        Assert.EndsWith("Merge_Invoices_20260918103100.pdf", path);
    }

    [Fact]
    public void CreateOutputPath_IgnoresCurrentCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // The Thai culture uses the Buddhist calendar (year 2569 instead of 2026).
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");

            var path = _namer.CreateOutputPath(Folder);

            Assert.EndsWith("Merge_Invoices_20260918103005.pdf", path);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void IsOutputFileName_RecognisesNamesItCreates()
    {
        var fileName = Path.GetFileName(_namer.CreateOutputPath(Folder));

        Assert.True(_namer.IsOutputFileName(Folder, fileName));
    }

    [Theory]
    [InlineData("merge_invoices_20260918103005.PDF")]
    [InlineData("Merge_Invoices_20000101000000.pdf")]
    public void IsOutputFileName_IsCaseInsensitive_AndAcceptsAnyTimestamp(string fileName)
    {
        Assert.True(_namer.IsOutputFileName(Folder, fileName));
    }

    [Theory]
    [InlineData("Merge_Other_20260918103005.pdf")]
    [InlineData("Merge_Invoices_notes.pdf")]
    [InlineData("Merge_Invoices_2026091810300.pdf")]
    [InlineData("Merge_Invoices_202609181030055.pdf")]
    [InlineData("Merge_Invoices_20260918103005.pdf.bak")]
    [InlineData("xMerge_Invoices_20260918103005.pdf")]
    [InlineData("report.pdf")]
    public void IsOutputFileName_RejectsOtherNames(string fileName)
    {
        Assert.False(_namer.IsOutputFileName(Folder, fileName));
    }

    [Fact]
    public void IsOutputFileName_FolderNameWithRegexCharacters_IsMatchedLiterally()
    {
        var folder = @"C:\docs\a.b(1)";

        Assert.True(_namer.IsOutputFileName(folder, "Merge_a.b(1)_20260918103005.pdf"));
        Assert.False(_namer.IsOutputFileName(folder, "Merge_aXb(1)_20260918103005.pdf"));
    }
}
