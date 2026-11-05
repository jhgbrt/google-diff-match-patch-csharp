using System.Buffers;
using static DiffMatchPatch.Operation;

namespace DiffMatchPatch;

public record Patch(int Start1, int Length1, int Start2, int Length2, ImmutableListWithValueSemantics<Diff> Diffs)
{
    public Patch Bump(int length) => this with { Start1 = Start1 + length, Start2 = Start2 + length };

    public bool IsEmpty => Diffs.IsEmpty;
    public bool StartsWith(Operation operation) => Diffs[0].Operation == operation;
    public bool EndsWith(Operation operation) => Diffs[^1].Operation == operation;

    internal Patch AddPaddingInFront(string padding)
    {
        (var s1, var l1, var s2, var l2, var diffs) = this;

        var builder = diffs.ToBuilder();
        (s1, l1, s2, l2) = AddPaddingInFront(builder, s1, l1, s2, l2, padding);

        return new Patch(s1, l1, s2, l2, builder.ToImmutable());
    }

    internal Patch AddPaddingAtEnd(string padding)
    {
        (var s1, var l1, var s2, var l2, var diffs) = this;

        var builder = diffs.ToBuilder();
        (s1, l1, s2, l2) = AddPaddingAtEnd(builder, s1, l1, s2, l2, padding);

        return new Patch(s1, l1, s2, l2, builder.ToImmutable());
    }

    internal Patch AddPadding(string padding)
    {
        (var s1, var l1, var s2, var l2, var diffs) = this;

        var builder = diffs.ToBuilder();
        (s1, l1, s2, l2) = AddPaddingInFront(builder, s1, l1, s2, l2, padding);
        (s1, l1, s2, l2) = AddPaddingAtEnd(builder, s1, l1, s2, l2, padding);

        return new Patch(s1, l1, s2, l2, builder.ToImmutable());
    }

    private (int s1, int l1, int s2, int l2) AddPaddingInFront(ImmutableList<Diff>.Builder builder, int s1, int l1, int s2, int l2, string padding)
    {
        if (!StartsWith(Equal))
        {
            builder.Insert(0, Diff.Equal(padding));
            return (s1 - padding.Length, l1 + padding.Length, s2 - padding.Length, l2 + padding.Length);
        }
        else if (padding.Length > Diffs[0].Text.Length)
        {
            var firstDiff = Diffs[0];
            var extraLength = padding.Length - firstDiff.Text.Length;
            var text = padding[firstDiff.Text.Length..] + firstDiff.Text;

            builder.RemoveAt(0);
            builder.Insert(0, firstDiff.Replace(text));
            return (s1 - extraLength, l1 + extraLength, s2 - extraLength, l2 + extraLength);
        }
        else
        {
            return (s1, l1, s2, l2);
        }

    }

    private (int s1, int l1, int s2, int l2) AddPaddingAtEnd(ImmutableList<Diff>.Builder builder, int s1, int l1, int s2, int l2, string padding)
    {
        if (!EndsWith(Equal))
        {
            builder.Add(Diff.Equal(padding));
            return (s1, l1 + padding.Length, s2, l2 + padding.Length);
        }
        else if (padding.Length > Diffs[^1].Text.Length)
        {
            var lastDiff = Diffs[^1];
            var extraLength = padding.Length - lastDiff.Text.Length;
            var text = lastDiff.Text + padding[..extraLength];

            builder.RemoveAt(builder.Count - 1);
            builder.Add(lastDiff.Replace(text));

            return (s1, l1 + extraLength, s2, l2 + extraLength);
        }
        else
        {
            return (s1, l1, s2, l2);
        }

    }

    /// <summary>
    /// Generate GNU diff's format.
    /// Header: @@ -382,8 +481,9 @@
    /// Indicies are printed as 1-based, not 0-based.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {

        var coords1 = Length1 switch
        {
            0 => Start1 + ",0",
            1 => Convert.ToString(Start1 + 1),
            _ => Start1 + 1 + "," + Length1
        };

        var coords2 = Length2 switch
        {
            0 => Start2 + ",0",
            1 => Convert.ToString(Start2 + 1),
            _ => Start2 + 1 + "," + Length2
        };

        var text = new StringBuilder()
            .Append("@@ -")
            .Append(coords1)
            .Append(" +")
            .Append(coords2)
            .Append(" @@\n");

        // Escape the body of the patch with %xx notation.
        foreach (var aDiff in Diffs)
        {
            text.Append((char)aDiff.Operation);
            text.AppendUrlEncoded(aDiff.TextSpan).Append('\n');
        }

        return text.ToString();
    }
    /// <summary>
    /// Compute a list of patches to turn text1 into text2.
    /// A set of Diffs will be computed.
    /// </summary>
    /// <param name="text1">old text</param>
    /// <param name="text2">new text</param>
    /// <param name="diffTimeout">timeout in seconds</param>
    /// <param name="diffEditCost">Cost of an empty edit operation in terms of edit characters.</param>
    /// <returns>List of Patch objects</returns>
    public static ImmutableListWithValueSemantics<Patch> Compute(string text1, string text2, float diffTimeout = 0, short diffEditCost = 4)
    {
        using var cts = diffTimeout <= 0
            ? new CancellationTokenSource()
            : new CancellationTokenSource(TimeSpan.FromSeconds(diffTimeout));
        using var engine = new DiffEngine(text1, text2, diffTimeout > 0, cts.Token);
        engine.Compute(checklines: true);
        engine.CleanupSemantic();
        engine.CleanupEfficiency(diffEditCost);
        return Compute(text1, engine.ToList()).ToImmutableList().WithValueSemantics();
    }

    /// <summary>
    /// Compute a list of patches to turn text1 into text2.
    /// text1 will be derived from the provided Diffs.
    /// </summary>
    /// <param name="diffs">array of diff objects for text1 to text2</param>
    /// <returns>List of Patch objects</returns>
    public static ImmutableListWithValueSemantics<Patch> FromDiffs(IEnumerable<Diff> diffs)
        => Compute(diffs.Text1(), diffs).ToImmutableList().WithValueSemantics();

    /// <summary>
    /// Compute a list of patches to turn text1 into text2.
    /// text2 is not provided, Diffs are the delta between text1 and text2.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="diffs"></param>
    /// <param name="patchMargin"></param>
    /// <returns></returns>
    public static IEnumerable<Patch> Compute(string text1, IEnumerable<Diff> input, short patchMargin = 4)
    {
        var diffs = input as IReadOnlyList<Diff> ?? input.ToList();
        if (diffs.Count == 0)
        {
            yield break;  // Get rid of the null case.
        }

        // Length of text2 (the result of applying all diffs to text1).
        var text2Length = text1.Length;
        foreach (var d in diffs)
        {
            if (d.Operation == Insert) text2Length += d.TextLength;
            else if (d.Operation == Delete) text2Length -= d.TextLength;
        }

        // Start with text1 (prepatch text) and apply the diffs until we arrive at
        // text2 (postpatch text). We recreate the patches one by one to determine
        // context info. Unlike Unidiff, our patch lists have a rolling context:
        // each patch's context is taken from the text with all previous patches applied.
        // After applying the diffs up to position o1 in text1 and o2 in text2, that text is
        // text2[..o2] + text1[o1..]. text2 is built up in a buffer as the diffs are processed.
        var pool = ArrayPool<char>.Shared;
        var text2 = pool.Rent(Math.Max(1, text2Length));
        var prepatch = pool.Rent(Math.Max(1, text1.Length + text2Length));
        try
        {
            text1.CopyTo(0, prepatch, 0, text1.Length);
            var prepatchLength = text1.Length;
            var original1 = 0; // position in text1
            var original2 = 0; // position in text2

            var charCount1 = 0;  // Number of characters into the prepatch text.
            var charCount2 = 0;  // Number of characters into the postpatch text.
            var newdiffs = ImmutableList.CreateBuilder<Diff>();
            int start1 = 0, length1 = 0, start2 = 0, length2 = 0;
            for (var index = 0; index < diffs.Count; index++)
            {
                var aDiff = diffs[index];
                if (newdiffs.Count == 0 && aDiff.Operation != Equal)
                {
                    // A new patch starts here.
                    start1 = charCount1;
                    start2 = charCount2;
                }

                switch (aDiff.Operation)
                {
                    case Insert:
                        newdiffs.Add(aDiff);
                        length2 += aDiff.TextLength;
                        break;
                    case Delete:
                        length1 += aDiff.TextLength;
                        newdiffs.Add(aDiff);
                        break;
                    case Equal:
                        if (aDiff.TextLength <= 2 * patchMargin && newdiffs.Count > 0 && index != diffs.Count - 1)
                        {
                            // Small equality inside a patch.
                            newdiffs.Add(aDiff);
                            length1 += aDiff.TextLength;
                            length2 += aDiff.TextLength;
                        }

                        if (aDiff.TextLength >= 2 * patchMargin && newdiffs.Count > 0)
                        {
                            // Time for a new patch.
                            (start1, length1, start2, length2) = newdiffs.AddContext(prepatch.AsSpan(0, prepatchLength), start1, length1, start2, length2, patchMargin);
                            yield return new Patch(start1, length1, start2, length2, newdiffs.ToImmutable());
                            start1 = start2 = length1 = length2 = 0;
                            newdiffs.Clear();
                            // Update prepatch text & pos to reflect the application of the
                            // just completed patch: text2[..original2] + text1[original1..]
                            text2.AsSpan(0, original2).CopyTo(prepatch);
                            text1.CopyTo(original1, prepatch, original2, text1.Length - original1);
                            prepatchLength = original2 + text1.Length - original1;
                            charCount1 = charCount2;
                        }
                        break;
                }

                // Update the current character counts.
                if (aDiff.Operation != Insert)
                {
                    charCount1 += aDiff.TextLength;
                    original1 += aDiff.TextLength;
                }
                if (aDiff.Operation != Delete)
                {
                    charCount2 += aDiff.TextLength;
                    aDiff.TextSpan.CopyTo(text2.AsSpan(original2));
                    original2 += aDiff.TextLength;
                }
            }
            // Pick up the leftover patch if not empty.
            if (newdiffs.Count > 0)
            {
                (start1, length1, start2, length2) = newdiffs.AddContext(prepatch.AsSpan(0, prepatchLength), start1, length1, start2, length2, patchMargin);
                yield return new Patch(start1, length1, start2, length2, newdiffs.ToImmutable());
            }
        }
        finally
        {
            pool.Return(text2);
            pool.Return(prepatch);
        }
    }

}
