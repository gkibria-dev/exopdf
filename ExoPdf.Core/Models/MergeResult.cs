namespace ExoPdf.Core.Models;

public class MergeResult
{
    public required string OutputFilePath { get; init; }
    public int FilesMerged { get; init; }
    public int TotalPages { get; init; }
}
