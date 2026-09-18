using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExoPdf.Desktop.Models;

namespace ExoPdf.Desktop.Services;

public class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public JsonSettingsStore(string filePath)
    {
        _filePath = filePath;
        Current = Load();
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ExoPdf",
        "settings.json");

    public AppSettings Current { get; }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience; failing to save them must not disturb the user.
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_filePath), JsonOptions)
                ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }
}
