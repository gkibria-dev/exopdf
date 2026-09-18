using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

/// <summary>Reads and writes the settings document. Knows nothing about who uses the settings.</summary>
public interface ISettingsStore
{
    /// <summary>Loads the saved settings, or defaults if there are none or they cannot be read.</summary>
    AppSettings Load();

    void Save(AppSettings settings);
}
