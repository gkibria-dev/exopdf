using System.IO.Abstractions;
using ExoPdf.Core.Errors;

namespace ExoPdf.Core.Merging;

public sealed class MergeSourceFinder(IFileSystem fileSystem, MergeOutputNamer namer) : IMergeSourceFinder
{
    public IReadOnlyList<string> Find(string folderPath)
    {
        if (!fileSystem.Directory.Exists(folderPath))
            throw new SourceFolderNotFoundException(folderPath);

        try
        {
            return fileSystem.Directory.GetFiles(folderPath, "*.pdf")
                .Where(file => !namer.IsOutputFileName(folderPath, fileSystem.Path.GetFileName(file)))
                .OrderBy(file => fileSystem.Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ExoPdfException($"Cannot read the folder \"{folderPath}\": {ex.Message}", ex);
        }
    }
}
