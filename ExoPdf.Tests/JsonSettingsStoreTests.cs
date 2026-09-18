using ExoPdf.Desktop.Models;
using ExoPdf.Desktop.Services;

namespace ExoPdf.Tests;

public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private string FilePath => Path.Combine(_folder, "ExoPdf", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    private void WriteFile(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, content);
    }

    [Fact]
    public void MissingFile_GivesDefaults()
    {
        var store = new JsonSettingsStore(FilePath);

        Assert.Equal(AppTheme.System, store.Current.Theme);
        Assert.Null(store.Current.LastMergeFolder);
    }

    [Fact]
    public void Save_CreatesFolderAndPersistsSettings()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Current.Theme = AppTheme.Dark;
        store.Current.LastMergeFolder = @"C:\Docs";

        store.Save();
        var reloaded = new JsonSettingsStore(FilePath);

        Assert.Equal(AppTheme.Dark, reloaded.Current.Theme);
        Assert.Equal(@"C:\Docs", reloaded.Current.LastMergeFolder);
    }

    [Fact]
    public void Save_WritesThemeAsReadableText()
    {
        var store = new JsonSettingsStore(FilePath);
        store.Current.Theme = AppTheme.Dark;

        store.Save();

        Assert.Contains("\"Dark\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Save_LeavesNoTemporaryFileBehind()
    {
        var store = new JsonSettingsStore(FilePath);

        store.Save();

        Assert.Equal([FilePath], Directory.GetFiles(Path.GetDirectoryName(FilePath)!));
    }

    [Fact]
    public void CorruptFile_GivesDefaults()
    {
        WriteFile("{ this is not json");

        var store = new JsonSettingsStore(FilePath);

        Assert.Equal(AppTheme.System, store.Current.Theme);
    }

    [Fact]
    public void UnknownThemeValue_GivesDefaults()
    {
        WriteFile("""{ "Theme": "Purple" }""");

        var store = new JsonSettingsStore(FilePath);

        Assert.Equal(AppTheme.System, store.Current.Theme);
    }

    [Fact]
    public void EmptyFile_GivesDefaults()
    {
        WriteFile("");

        var store = new JsonSettingsStore(FilePath);

        Assert.Equal(AppTheme.System, store.Current.Theme);
    }

    [Fact]
    public void UnknownProperties_AreIgnored_AndKnownOnesStillLoad()
    {
        WriteFile("""{ "Theme": "Light", "SomethingFromANewerVersion": 42 }""");

        var store = new JsonSettingsStore(FilePath);

        Assert.Equal(AppTheme.Light, store.Current.Theme);
    }

    [Fact]
    public void MissingProperties_FallBackToDefaults()
    {
        WriteFile("""{ "LastMergeFolder": "C:\\Docs" }""");

        var store = new JsonSettingsStore(FilePath);

        Assert.Equal(AppTheme.System, store.Current.Theme);
        Assert.Equal(@"C:\Docs", store.Current.LastMergeFolder);
    }

    [Fact]
    public void Save_WhenFolderCannotBeCreated_DoesNotThrow()
    {
        Directory.CreateDirectory(_folder);
        var blocker = Path.Combine(_folder, "blocker");
        File.WriteAllText(blocker, "a file where a folder is needed");
        var store = new JsonSettingsStore(Path.Combine(blocker, "settings.json"));

        var exception = Record.Exception(store.Save);

        Assert.Null(exception);
    }

    [Fact]
    public void DefaultFilePath_IsUnderAppDataExoPdf()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExoPdf", "settings.json");

        Assert.Equal(expected, JsonSettingsStore.DefaultFilePath);
    }
}
