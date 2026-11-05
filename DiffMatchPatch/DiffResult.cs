namespace DiffMatchPatch;

/// <summary>
/// The result of <see cref="Diff.Compute(string, string, float, bool)"/>: an immutable list of diffs.
/// </summary>
public readonly struct DiffResult : IReadOnlyList<Diff>
{
    private readonly ImmutableArray<Diff> _diffs;

    public DiffResult(ImmutableArray<Diff> diffs) => _diffs = diffs;

    /// <summary>
    /// A result for which the source texts are known, so that cleanup operations
    /// do not need to reconstruct them from the diffs.
    /// </summary>
    internal DiffResult(ImmutableArray<Diff> diffs, string text1, string text2)
    {
        _diffs = diffs;
        SourceText = text1;
        DestinationText = text2;
    }

    /// <summary>The source text, if known (otherwise null).</summary>
    internal string? SourceText { get; }

    /// <summary>The destination text, if known (otherwise null).</summary>
    internal string? DestinationText { get; }

    public DiffResult(IEnumerable<Diff> diffs) => _diffs = diffs.ToImmutableArray();

    private ImmutableArray<Diff> Diffs => _diffs.IsDefault ? ImmutableArray<Diff>.Empty : _diffs;

    public int Count => Diffs.Length;

    public Diff this[int index] => Diffs[index];

    public ImmutableList<Diff> ToImmutableList() => Diffs.ToImmutableList();

    public IEnumerator<Diff> GetEnumerator() => ((IEnumerable<Diff>)Diffs).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
