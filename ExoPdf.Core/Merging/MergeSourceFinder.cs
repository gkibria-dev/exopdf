using System.IO.Abstractions;

namespace ExoPdf.Core.Merging;

public sealed class MergeSourceFinder(IFileSystem fileSystem, MergeOutputNamer namer) : IMergeSourceFinder
{
    public IReadOnlyList<string> Find(string folderPath)
    {
        if (!fileSystem.Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

        return fileSystem.Directory.GetFiles(folderPath, "*.pdf")
            .Where(file => !namer.IsOutputFileName(folderPath, fileSystem.Path.GetFileName(file)))
            .OrderBy(file => fileSystem.Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
