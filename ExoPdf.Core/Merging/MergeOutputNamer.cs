using System.Globalization;
using System.IO.Abstractions;
using System.Text.RegularExpressions;

namespace ExoPdf.Core.Merging;

/// <summary>
/// The single owner of the merge output naming rule,
/// <c>Merge_&lt;FolderName&gt;_&lt;yyyyMMddHHmmss&gt;.pdf</c>: it creates output paths
/// and recognises them again, so the two can never drift apart.
/// </summary>
public sealed class MergeOutputNamer(IFileSystem fileSystem, TimeProvider timeProvider)
{
    private const string TimestampFormat = "yyyyMMddHHmmss";

    /// <summary>
    /// Returns a path for a new output file in <paramref name="folderPath"/> that does not
    /// exist yet. If the name for the current second is taken, the timestamp advances by
    /// one second until a free name is found.
    /// </summary>
    public string CreateOutputPath(string folderPath)
    {
        var folderName = GetFolderName(folderPath);
        var timestamp = timeProvider.GetLocalNow().DateTime;

        while (true)
        {
            var stamp = timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            var path = fileSystem.Path.Combine(folderPath, $"Merge_{folderName}_{stamp}.pdf");
            if (!fileSystem.File.Exists(path))
                return path;

            timestamp = timestamp.AddSeconds(1);
        }
    }

    /// <summary>Whether <paramref name="fileName"/> is an output of a previous merge of <paramref name="folderPath"/>.</summary>
    public bool IsOutputFileName(string folderPath, string fileName) =>
        Regex.IsMatch(
            fileName,
            $"^Merge_{Regex.Escape(GetFolderName(folderPath))}_[0-9]{{{TimestampFormat.Length}}}[.]pdf$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private string GetFolderName(string folderPath) =>
        fileSystem.Path.GetFileName(fileSystem.Path.TrimEndingDirectorySeparator(folderPath));
}
