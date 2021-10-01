namespace DiffMatchPatch;

public readonly record struct Diff(Operation Operation, string Text)
{
    internal static Diff Create(Operation operation, string text) => new(operation, text);
    public static Diff Equal(ReadOnlySpan<char> text) => Create(Operation.Equal, text.ToString());
    public static Diff Insert(ReadOnlySpan<char> text) => Create(Operation.Insert, text.ToString());
    public static Diff Delete(ReadOnlySpan<char> text) => Create(Operation.Delete, text.ToString());
    public static Diff Empty => new(Operation.Equal, string.Empty);
    /// <summary>
    /// Generate a human-readable version of this Diff.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        var prettyText = Text.Replace('\n', '\u00b6');
        return "Diff(" + Operation + ",\"" + prettyText + "\")";
    }

    internal Diff Replace(string text) => this with { Text = text };
    internal Diff Append(string text) => this with { Text = Text + text };
    internal Diff Prepend(string text) => this with { Text = text + Text };

    public bool IsEmpty => Text.Length == 0;

    /// <summary>
    /// Find the differences between two texts.
    /// </summary>
    /// <param name="text1">Old string to be diffed</param>
    /// <param name="text2">New string to be diffed</param>
    /// <param name="timeoutInSeconds">if specified, certain optimizations may be enabled to meet the time constraint, possibly resulting in a less optimal diff</param>
    /// <param name="checklines">If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
    /// <returns></returns>
    public static ImmutableList<Diff> Compute(string text1, string text2, float timeoutInSeconds = 0f, bool checklines = true)
    {
        using var cts = timeoutInSeconds <= 0
            ? new CancellationTokenSource()
            : new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds));
        return Compute(text1, text2, checklines, timeoutInSeconds > 0, cts.Token);
    }

    public static ImmutableList<Diff> Compute(string text1, string text2, bool checkLines, bool optimizeForSpeed, CancellationToken token)
        => DiffAlgorithm.Compute(text1, text2, checkLines, optimizeForSpeed, token).ToImmutableList();

    public bool IsLargeDelete(int size) => Operation == Operation.Delete && Text.Length > size;

}
