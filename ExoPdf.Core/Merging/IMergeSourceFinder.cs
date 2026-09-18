namespace ExoPdf.Core.Merging;

public interface IMergeSourceFinder
{
    /// <summary>
    /// Returns the PDF files a merge of <paramref name="folderPath"/> would combine, in
    /// merge order: ascending file name, case-insensitive. Output files from previous
    /// merges of the same folder are excluded.
    /// </summary>
    IReadOnlyList<string> Find(string folderPath);
}
