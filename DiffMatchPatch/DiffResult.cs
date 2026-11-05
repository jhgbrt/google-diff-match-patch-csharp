namespace DiffMatchPatch;

/// <summary>
/// The result of <see cref="Diff.Compute(string, string, float, bool)"/>: an immutable list of diffs.
/// </summary>
public readonly struct DiffResult : IReadOnlyList<Diff>
{
    private readonly ImmutableArray<Diff> _diffs;

    public DiffResult(ImmutableArray<Diff> diffs) => _diffs = diffs;

    public DiffResult(IEnumerable<Diff> diffs) => _diffs = diffs.ToImmutableArray();

    private ImmutableArray<Diff> Diffs => _diffs.IsDefault ? ImmutableArray<Diff>.Empty : _diffs;

    public int Count => Diffs.Length;

    public Diff this[int index] => Diffs[index];

    public ImmutableList<Diff> ToImmutableList() => Diffs.ToImmutableList();

    public IEnumerator<Diff> GetEnumerator() => ((IEnumerable<Diff>)Diffs).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
