using System.IO;
using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

public class JsonSettingsStore(IFileSystem fileSystem, string filePath) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExoPdf",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!fileSystem.File.Exists(filePath))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(fileSystem.File.ReadAllText(filePath), JsonOptions)
                ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            fileSystem.Directory.CreateDirectory(fileSystem.Path.GetDirectoryName(filePath)!);

            // Write beside the settings file, then replace it, so a crash cannot leave half a file.
            var tempPath = filePath + ".tmp";
            fileSystem.File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
            fileSystem.File.Move(tempPath, filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience; failing to save them must not disturb the user.
        }
    }
}
