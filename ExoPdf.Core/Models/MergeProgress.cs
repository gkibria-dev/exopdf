namespace ExoPdf.Core.Models;

/// <summary>Reported after each source file has been merged.</summary>
/// <param name="FilesCompleted">Files merged so far, including <paramref name="CurrentFile"/>.</param>
/// <param name="TotalFiles">Files in this merge.</param>
/// <param name="CurrentFile">Name of the file that was just merged.</param>
public sealed record MergeProgress(int FilesCompleted, int TotalFiles, string CurrentFile);
