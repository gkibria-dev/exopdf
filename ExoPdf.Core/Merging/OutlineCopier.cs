using PdfSharp.Pdf;

namespace ExoPdf.Core.Merging;

/// <summary>Copies the bookmarks of an imported document into the merged output.</summary>
internal static class OutlineCopier
{
    /// <summary>
    /// Copies the bookmarks of <paramref name="input"/> as children of
    /// <paramref name="target"/>, remapping each destination to its page in
    /// <paramref name="output"/>. Bookmarks without a page destination are skipped.
    /// </summary>
    public static void Copy(PdfDocument input, PdfOutlineCollection target, PdfDocument output, int pageOffset)
    {
        if (input.Outlines.Count == 0)
            return;

        var pageIndexMap = BuildPageIndexMap(input);
        Copy(input.Outlines, target, output, pageIndexMap, pageOffset);
    }

    private static Dictionary<PdfPage, int> BuildPageIndexMap(PdfDocument input)
    {
        var map = new Dictionary<PdfPage, int>();
        for (int i = 0; i < input.PageCount; i++)
            map[input.Pages[i]] = i;
        return map;
    }

    private static void Copy(
        PdfOutlineCollection source,
        PdfOutlineCollection target,
        PdfDocument output,
        Dictionary<PdfPage, int> pageIndexMap,
        int pageOffset)
    {
        foreach (PdfOutline outline in source)
        {
            if (outline.DestinationPage == null || !pageIndexMap.TryGetValue(outline.DestinationPage, out int inputPageIndex))
                continue;

            int outputPageIndex = pageOffset + inputPageIndex;
            if (outputPageIndex >= output.PageCount)
                continue;

            var child = target.Add(outline.Title, output.Pages[outputPageIndex]);
            child.PageDestinationType = outline.PageDestinationType;

            if (outline.Outlines.Count > 0)
                Copy(outline.Outlines, child.Outlines, output, pageIndexMap, pageOffset);
        }
    }
}
