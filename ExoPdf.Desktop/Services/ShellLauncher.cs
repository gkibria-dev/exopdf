using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace ExoPdf.Desktop.Services;

public class ShellLauncher : IShellLauncher
{
    public void OpenFile(string filePath) =>
        Start(new ProcessStartInfo(filePath) { UseShellExecute = true });

    // Explorer expects /select,"<path>" with only the path quoted. ArgumentList would
    // quote the whole switch when the path contains spaces, which Explorer misreads.
    // Windows file names cannot contain a double quote, so the path cannot break out.
    public void ShowInFolder(string filePath) =>
        Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/select,\"{filePath}\"" });

    private static void Start(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            throw new ShellLaunchException(ex.Message, ex);
        }
    }
}
