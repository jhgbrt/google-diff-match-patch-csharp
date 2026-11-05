namespace DiffMatchPatch;

/// <summary>
/// Entry points for the diff algorithm, which is implemented by <see cref="DiffEngine"/>.
/// </summary>
static class DiffAlgorithm
{
    /// <summary>
    /// Find the differences between two texts.
    /// </summary>
    internal static List<Diff> Compute(StringSlice text1, StringSlice text2, bool checklines, bool optimizeForSpeed, CancellationToken token)
    {
        using var engine = new DiffEngine(text1.Source ?? string.Empty, text2.Source ?? string.Empty, optimizeForSpeed, token);
        engine.Compute(text1.Offset, text1.Length, text2.Offset, text2.Length, checklines);
        return engine.ToList();
    }

    /// <summary>
    /// Find the 'middle snake' of a diff, split the problem in two
    /// and return the recursively constructed diff.
    /// See Myers 1986 paper: An O(ND) Difference Algorithm and Its Variations.
    /// </summary>
    internal static List<Diff> MyersDiffBisect(StringSlice text1, StringSlice text2, bool optimizeForSpeed, CancellationToken token)
    {
        using var engine = new DiffEngine(text1.Source ?? string.Empty, text2.Source ?? string.Empty, optimizeForSpeed, token);
        engine.Bisect(text1.Offset, text1.Length, text2.Offset, text2.Length);
        return engine.ToList();
    }
}
