namespace ExoPdf.Core.Errors;

/// <summary>The merged file could not be written, for example because the folder is read-only.</summary>
public sealed class OutputWriteException(string folderPath, Exception innerException)
    : ExoPdfException($"Cannot write the merged file to \"{folderPath}\": {innerException.Message}", innerException)
{
    public string FolderPath { get; } = folderPath;
}
