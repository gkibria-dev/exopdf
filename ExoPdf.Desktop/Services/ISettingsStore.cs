using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

public interface ISettingsStore
{
    /// <summary>The settings in use. Change properties, then call <see cref="Save"/>.</summary>
    AppSettings Current { get; }

    void Save();
}
