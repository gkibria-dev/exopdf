namespace ExoPdf.Desktop.Services;

public interface IFolderPicker
{
    /// <summary>Shows a folder dialog. Returns the chosen path, or null if cancelled.</summary>
    string? PickFolder(string? initialFolder);
}
