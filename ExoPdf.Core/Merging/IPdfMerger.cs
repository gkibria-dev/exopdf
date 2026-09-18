using ExoPdf.Core.Models;

namespace ExoPdf.Core.Merging;

public interface IPdfMerger
{
    /// <summary>
    /// Merges the PDFs described by <paramref name="options"/> into one file in the source folder.
    /// </summary>
    /// <param name="progress">Receives a report after each file is merged.</param>
    /// <param name="cancellationToken">
    /// Checked before each file and before writing. Cancelling throws
    /// <see cref="OperationCanceledException"/> and leaves no output file.
    /// </param>
    /// <exception cref="ExoPdf.Core.Errors.ExoPdfException">An expected failure; the message is fit to show to the user.</exception>
    MergeResult Merge(
        MergeOptions options,
        IProgress<MergeProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
