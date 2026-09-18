namespace ExoPdf.Core.Models;

public class MergeOptions
{
    /// <summary>
    /// The folder to merge, and the folder the output is written to.
    /// </summary>
    public required string SourceFolderPath { get; init; }

    /// <summary>
    /// An explicit, ordered list of files to merge. When null, the PDFs in
    /// <see cref="SourceFolderPath"/> are found and ordered automatically.
    /// </summary>
    public IReadOnlyList<string>? Files { get; init; }
}
