using System.IO;
using Microsoft.Win32;

namespace ExoPdf.Desktop.Services;

public class FolderPicker : IFolderPicker
{
    public string? PickFolder(string? initialFolder)
    {
        var dialog = new OpenFolderDialog { Title = "Select a folder containing PDF files" };

        if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
            dialog.InitialDirectory = initialFolder;

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
