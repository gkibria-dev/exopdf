namespace ExoPdf.Desktop.Services;

public interface IShellLauncher
{
    void OpenFile(string filePath);

    void ShowInFolder(string filePath);
}
