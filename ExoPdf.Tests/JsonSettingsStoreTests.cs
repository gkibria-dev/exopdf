using System.IO.Abstractions.TestingHelpers;
using ExoPdf.Desktop.Models;
using ExoPdf.Desktop.Services;

namespace ExoPdf.Tests;

public class JsonSettingsStoreTests
{
    private const string FilePath = @"C:\Users\me\AppData\Roaming\ExoPdf\settings.json";

    private readonly MockFileSystem _fileSystem = new();
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests() => _store = new JsonSettingsStore(_fileSystem, FilePath);

    private void WriteFile(string content) => _fileSystem.AddFile(FilePath, new MockFileData(content));

    [Fact]
    public void Load_MissingFile_GivesDefaults()
    {
        var settings = _store.Load();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Null(settings.LastMergeFolder);
    }

    [Fact]
    public void Save_CreatesFolderAndPersistsSettings()
    {
        _store.Save(new AppSettings { Theme = AppTheme.Dark, LastMergeFolder = @"C:\Docs" });

        var reloaded = _store.Load();

        Assert.Equal(AppTheme.Dark, reloaded.Theme);
        Assert.Equal(@"C:\Docs", reloaded.LastMergeFolder);
    }

    [Fact]
    public void Save_WritesThemeAsReadableText()
    {
        _store.Save(new AppSettings { Theme = AppTheme.Dark });

        Assert.Contains("\"Dark\"", _fileSystem.File.ReadAllText(FilePath));
    }

    [Fact]
    public void Save_ReplacesAnExistingFile()
    {
        _store.Save(new AppSettings { Theme = AppTheme.Dark });

        _store.Save(new AppSettings { Theme = AppTheme.Light });

        Assert.Equal(AppTheme.Light, _store.Load().Theme);
    }

    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        _store.Save(new AppSettings());

        Assert.Equal([FilePath], _fileSystem.Directory.GetFiles(Path.GetDirectoryName(FilePath)!));
    }

    [Fact]
    public void Load_CorruptFile_GivesDefaults()
    {
        WriteFile("{ this is not json");

        Assert.Equal(AppTheme.System, _store.Load().Theme);
    }

    [Fact]
    public void Load_UnknownThemeValue_GivesDefaults()
    {
        WriteFile("""{ "Theme": "Purple" }""");

        Assert.Equal(AppTheme.System, _store.Load().Theme);
    }

    [Fact]
    public void Load_EmptyFile_GivesDefaults()
    {
        WriteFile("");

        Assert.Equal(AppTheme.System, _store.Load().Theme);
    }

    [Fact]
    public void Load_JsonNull_GivesDefaults()
    {
        WriteFile("null");

        Assert.Equal(AppTheme.System, _store.Load().Theme);
    }

    [Fact]
    public void Load_UnknownProperties_AreIgnored_AndKnownOnesStillLoad()
    {
        WriteFile("""{ "Theme": "Light", "SomethingFromANewerVersion": 42 }""");

        Assert.Equal(AppTheme.Light, _store.Load().Theme);
    }

    [Fact]
    public void Load_MissingProperties_FallBackToDefaults()
    {
        WriteFile("""{ "LastMergeFolder": "C:\\Docs" }""");

        var settings = _store.Load();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Equal(@"C:\Docs", settings.LastMergeFolder);
    }

    [Fact]
    public void Save_WhenTheFileCannotBeWritten_DoesNotThrowAndKeepsTheOldFile()
    {
        _store.Save(new AppSettings { Theme = AppTheme.Dark });
        // A read-only temporary file makes the next save fail.
        _fileSystem.AddFile(FilePath + ".tmp", new MockFileData("") { Attributes = FileAttributes.ReadOnly });

        var exception = Record.Exception(() => _store.Save(new AppSettings { Theme = AppTheme.Light }));

        Assert.Null(exception);
        Assert.Equal(AppTheme.Dark, _store.Load().Theme);
    }

    [Fact]
    public void DefaultFilePath_IsUnderAppDataExoPdf()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExoPdf", "settings.json");

        Assert.Equal(expected, JsonSettingsStore.DefaultFilePath);
    }

    [Fact]
    public void Constructing_DoesNotTouchTheFileSystem()
    {
        var fileSystem = new MockFileSystem();

        _ = new JsonSettingsStore(fileSystem, FilePath);

        Assert.Empty(fileSystem.AllFiles);
        Assert.Empty(fileSystem.AllDirectories.Where(d => d.Contains("ExoPdf")));
    }
}
