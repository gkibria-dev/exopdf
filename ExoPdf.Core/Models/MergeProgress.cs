namespace ExoPdf.Core.Models;

/// <summary>The phase a merge is in.</summary>
public enum MergeStage
{
    /// <summary>Source files are being read and merged. Cancelling is possible between files.</summary>
    Merging,

    /// <summary>Every file has been merged and the output is being written. This cannot be cancelled.</summary>
    Saving
}

/// <summary>Reported after each source file has been merged, and once more when saving starts.</summary>
/// <param name="FilesCompleted">Files merged so far, including <paramref name="CurrentFile"/>.</param>
/// <param name="TotalFiles">Files in this merge.</param>
/// <param name="CurrentFile">Name of the file that was just merged. Empty when saving.</param>
/// <param name="Stage">The phase the merge is in.</param>
public sealed record MergeProgress(
    int FilesCompleted,
    int TotalFiles,
    string CurrentFile,
    MergeStage Stage = MergeStage.Merging);
