namespace DiffMatchPatch;

internal static class DiffListBuilder
{
    /// <summary>
    /// Increase the context until it is unique,
    /// but don't let the pattern expand beyond Match_MaxBits.</summary>
    /// <param name="text">Source text</param>
    /// <param name="patchMargin"></param>
    internal static (int start1, int length1, int start2, int length2) AddContext(
        this ImmutableList<Diff>.Builder diffListBuilder,
        string text, int start1, int length1, int start2, int length2, short patchMargin = 4)
    {
        if (text.Length == 0)
        {
            return (start1, length1, start2, length2);
        }

        var pattern = text.Substring(start2, length1);
        var padding = 0;

        // Look for the first and last matches of pattern in text.  If two
        // different matches are found, increase the pattern length.
        while (text.IndexOf(pattern, StringComparison.Ordinal)
               != text.LastIndexOf(pattern, StringComparison.Ordinal)
               && pattern.Length < Constants.MatchMaxBits - patchMargin - patchMargin)
        {
            padding += patchMargin;
            var begin = Math.Max(0, start2 - padding);
            pattern = text[begin..Math.Min(text.Length, start2 + length1 + padding)];
        }
        // Add one chunk for good luck.
        padding += patchMargin;

        // Add the prefix.
        var begin1 = Math.Max(0, start2 - padding);
        var prefix = text[begin1..start2];
        if (prefix.Length != 0)
        {
            diffListBuilder.Insert(0, Diff.Equal(prefix));
        }
        // Add the suffix.
        var begin2 = start2 + length1;
        var length = Math.Min(text.Length, start2 + length1 + padding) - begin2;
        var suffix = text.Substring(begin2, length);
        if (suffix.Length != 0)
        {
            diffListBuilder.Add(Diff.Equal(suffix));
        }

        // Roll back the start points.
        start1 -= prefix.Length;
        start2 -= prefix.Length;
        // Extend the lengths.
        length1 = length1 + prefix.Length + suffix.Length;
        length2 = length2 + prefix.Length + suffix.Length;

        return (start1, length1, start2, length2);
    }
}
