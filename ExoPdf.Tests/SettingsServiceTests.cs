using ExoPdf.Desktop.Models;
using ExoPdf.Desktop.Services;

namespace ExoPdf.Tests;

public class SettingsServiceTests
{
    private readonly FakeSettingsStore _store = new();

    private SettingsService CreateService() => new(_store);

    [Fact]
    public void Constructing_DoesNotLoadYet()
    {
        CreateService();

        Assert.Equal(0, _store.LoadCount);
    }

    [Fact]
    public void Current_LoadsFromTheStoreOnceOnFirstUse()
    {
        _store.Stored = new AppSettings { Theme = AppTheme.Dark };
        var service = CreateService();

        var first = service.Current;
        var second = service.Current;

        Assert.Equal(AppTheme.Dark, first.Theme);
        Assert.Same(first, second);
        Assert.Equal(1, _store.LoadCount);
    }

    [Fact]
    public void Update_ReplacesTheSettingsAndSavesThem()
    {
        var service = CreateService();

        service.Update(s => s with { Theme = AppTheme.Light });

        Assert.Equal(AppTheme.Light, service.Current.Theme);
        Assert.Equal([AppTheme.Light], _store.Saved.Select(s => s.Theme));
    }

    [Fact]
    public void Update_KeepsSettingsTheChangeDidNotTouch()
    {
        _store.Stored = new AppSettings { Theme = AppTheme.Dark, RecentMergeFolders = [@"C:\Docs"] };
        var service = CreateService();

        service.Update(s => s with { Theme = AppTheme.Light });

        Assert.Equal([@"C:\Docs"], service.Current.RecentMergeFolders);
        Assert.Equal([@"C:\Docs"], _store.Saved.Single().RecentMergeFolders);
    }

    [Fact]
    public void Update_AppliesSuccessiveChangesToTheLatestState()
    {
        var service = CreateService();

        service.Update(s => s with { Theme = AppTheme.Dark });
        service.Update(s => s with { RecentMergeFolders = [@"C:\Docs"] });

        Assert.Equal(AppTheme.Dark, service.Current.Theme);
        Assert.Equal([@"C:\Docs"], service.Current.RecentMergeFolders);
        Assert.Equal(2, _store.Saved.Count);
    }

    [Fact]
    public void Update_WithNoRealChange_DoesNotSave()
    {
        _store.Stored = new AppSettings { Theme = AppTheme.Dark };
        var service = CreateService();

        service.Update(s => s with { Theme = AppTheme.Dark });

        Assert.Empty(_store.Saved);
    }

    [Fact]
    public void Update_LoadsBeforeChanging_SoAnUnloadedFileIsNotOverwrittenWithDefaults()
    {
        _store.Stored = new AppSettings { RecentMergeFolders = [@"C:\Docs"] };
        var service = CreateService();

        service.Update(s => s with { Theme = AppTheme.Dark });

        Assert.Equal([@"C:\Docs"], _store.Saved.Single().RecentMergeFolders);
    }
}
