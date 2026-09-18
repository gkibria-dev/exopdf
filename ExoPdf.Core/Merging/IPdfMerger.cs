using ExoPdf.Core.Models;

namespace ExoPdf.Core.Merging;

public interface IPdfMerger
{
    MergeResult Merge(MergeOptions options);
}
