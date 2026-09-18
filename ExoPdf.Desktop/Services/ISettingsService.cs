using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>
    /// Replaces the settings with the result of <paramref name="change"/> and saves them.
    /// Nothing is saved if the settings did not change.
    /// </summary>
    void Update(Func<AppSettings, AppSettings> change);
}
