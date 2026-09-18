using System.Diagnostics;

namespace ExoPdf.Desktop.Services;

public class ShellLauncher : IShellLauncher
{
    public void OpenFile(string filePath) =>
        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

    // Explorer expects /select,"<path>" with only the path quoted. ArgumentList would
    // quote the whole switch when the path contains spaces, which Explorer misreads.
    // Windows file names cannot contain a double quote, so the path cannot break out.
    public void ShowInFolder(string filePath) =>
        Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{filePath}\"" });
}
