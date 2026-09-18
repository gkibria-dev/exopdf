namespace ExoPdf.Core.Errors;

/// <summary>A source file could not be read as a PDF. The message names the file.</summary>
public sealed class PdfUnreadableException(string filePath, Exception innerException)
    : ExoPdfException($"Cannot read \"{Path.GetFileName(filePath)}\": {innerException.Message}", innerException)
{
    public string FilePath { get; } = filePath;
}
