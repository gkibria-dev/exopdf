namespace ExoPdf.Core.Errors;

public sealed class SourceFolderNotFoundException(string folderPath)
    : ExoPdfException($"Folder not found: {folderPath}")
{
    public string FolderPath { get; } = folderPath;
}
