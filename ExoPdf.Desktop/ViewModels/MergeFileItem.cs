using System.IO;

namespace ExoPdf.Desktop.ViewModels;

/// <summary>One PDF that will be merged. A class rather than a string so per-file state can be added later.</summary>
public class MergeFileItem
{
    public MergeFileItem(string fullPath)
    {
        FullPath = fullPath;
        Name = Path.GetFileName(fullPath);
    }

    public string FullPath { get; }

    public string Name { get; }
}
