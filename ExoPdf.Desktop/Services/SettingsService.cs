using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

/// <summary>Holds the settings in memory, loading them on first use and saving every change. Use from the UI thread.</summary>
public sealed class SettingsService(ISettingsStore store) : ISettingsService
{
    private AppSettings? _current;

    public AppSettings Current => _current ??= store.Load();

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var updated = change(Current);
        if (updated == Current)
            return;

        _current = updated;
        store.Save(updated);
    }
}
