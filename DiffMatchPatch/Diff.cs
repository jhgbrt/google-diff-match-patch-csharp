namespace DiffMatchPatch;

public readonly record struct Diff(Operation Operation, string Text)
{
    public ReadOnlySpan<char> TextSpan => Text.AsSpan();

    public int TextLength => Text?.Length ?? 0;

    internal static Diff Create(Operation operation, string text) => new(operation, text);

    public static Diff Equal(string text) => new(Operation.Equal, text);
    public static Diff Insert(string text) => new(Operation.Insert, text);
    public static Diff Delete(string text) => new(Operation.Delete, text);

    public static Diff Equal(ReadOnlySpan<char> text) => Equal(text.ToString());
    public static Diff Insert(ReadOnlySpan<char> text) => Insert(text.ToString());
    public static Diff Delete(ReadOnlySpan<char> text) => Delete(text.ToString());

    public static Diff Empty => Equal(string.Empty);

    internal Diff Replace(string text) => this with { Text = text };

    internal Diff Append(string text) => this with { Text = Text + text };

    internal Diff Prepend(string text) => this with { Text = text + Text };

    public bool IsEmpty => TextLength == 0;

    /// <summary>
    /// Generate a human-readable version of this Diff.
    /// </summary>
    public override string ToString()
    {
        var prettyText = Text.Replace('\n', '¶');
        return "Diff(" + Operation + ",\"" + prettyText + "\")";
    }

    /// <summary>
    /// Find the differences between two texts.
    /// </summary>
    /// <param name="text1">Old string to be diffed</param>
    /// <param name="text2">New string to be diffed</param>
    /// <param name="timeoutInSeconds">if specified, certain optimizations may be enabled to meet the time constraint, possibly resulting in a less optimal diff</param>
    /// <param name="checklines">If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
    public static DiffResult Compute(string text1, string text2, float timeoutInSeconds = 0f, bool checklines = true)
    {
        using var cts = timeoutInSeconds <= 0
            ? new CancellationTokenSource()
            : new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds));
        return Compute(text1, text2, checklines, timeoutInSeconds > 0, cts.Token);
    }

    public static DiffResult Compute(string text1, string text2, bool checkLines, bool optimizeForSpeed, CancellationToken token)
        => new DiffResult(DiffAlgorithm.Compute(new StringSlice(text1), new StringSlice(text2), checkLines, optimizeForSpeed, token));

    public bool IsLargeDelete(int size) => Operation == Operation.Delete && TextLength > size;
}
