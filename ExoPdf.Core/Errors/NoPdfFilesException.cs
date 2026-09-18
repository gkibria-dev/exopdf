namespace ExoPdf.Core.Errors;

public sealed class NoPdfFilesException(string folderPath)
    : ExoPdfException($"No PDF files to merge in: {folderPath}")
{
    public string FolderPath { get; } = folderPath;
}
