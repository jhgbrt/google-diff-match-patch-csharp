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
            var text = lastDiff.Text + padding.Substring(0, extraLength);

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
            text.Append(aDiff.Text.UrlEncoded()).Append("\n");
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
        return Compute(text1, DiffAlgorithm.Compute(text1, text2, true, true, cts.Token).CleanupSemantic().CleanupEfficiency(diffEditCost)).ToImmutableList().WithValueSemantics();
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
    public static IEnumerable<Patch> Compute(string text1, IEnumerable<Diff> diffs, short patchMargin = 4)
    {
        if (!diffs.Any())
        {
            yield break;  // Get rid of the null case.
        }

        var charCount1 = 0;  // Number of characters into the text1 string.
        var charCount2 = 0;  // Number of characters into the text2 string.
                             // Start with text1 (prepatch_text) and apply the Diffs until we arrive at
                             // text2 (postpatch_text). We recreate the patches one by one to determine
                             // context info.
        var prepatchText = text1;
        var postpatchText = text1;
        var newdiffs = ImmutableList.CreateBuilder<Diff>();
        int start1 = 0, length1 = 0, start2 = 0, length2 = 0;
        foreach (var aDiff in diffs)
        {
            if (!newdiffs.Any() && aDiff.Operation != Equal)
            {
                // A new patch starts here.
                start1 = charCount1;
                start2 = charCount2;
            }

            switch (aDiff.Operation)
            {
                case Insert:
                    newdiffs.Add(aDiff);
                    length2 += aDiff.Text.Length;
                    postpatchText = postpatchText.Insert(charCount2, aDiff.Text);
                    break;
                case Delete:
                    length1 += aDiff.Text.Length;
                    newdiffs.Add(aDiff);
                    postpatchText = postpatchText.Remove(charCount2, aDiff.Text.Length);
                    break;
                case Equal:
                    if (aDiff.Text.Length <= 2 * patchMargin && newdiffs.Any() && aDiff != diffs.Last())
                    {
                        // Small equality inside a patch.
                        newdiffs.Add(aDiff);
                        length1 += aDiff.Text.Length;
                        length2 += aDiff.Text.Length;
                    }

                    if (aDiff.Text.Length >= 2 * patchMargin)
                    {
                        // Time for a new patch.
                        if (newdiffs.Any())
                        {
                            (start1, length1, start2, length2) = newdiffs.AddContext(prepatchText, start1, length1, start2, length2);
                            yield return new Patch(start1, length1, start2, length2, newdiffs.ToImmutable());
                            start1 = start2 = length1 = length2 = 0;
                            newdiffs.Clear();
                            // Unlike Unidiff, our patch lists have a rolling context.
                            // http://code.google.com/p/google-diff-match-patch/wiki/Unidiff
                            // Update prepatch text & pos to reflect the application of the
                            // just completed patch.
                            prepatchText = postpatchText;
                            charCount1 = charCount2;
                        }
                    }
                    break;
            }

            // Update the current character count.
            if (aDiff.Operation != Insert)
            {
                charCount1 += aDiff.Text.Length;
            }
            if (aDiff.Operation != Delete)
            {
                charCount2 += aDiff.Text.Length;
            }
        }
        // Pick up the leftover patch if not empty.
        if (newdiffs.Any())
        {
            (start1, length1, start2, length2) = newdiffs.AddContext(prepatchText, start1, length1, start2, length2);
            yield return new Patch(start1, length1, start2, length2, newdiffs.ToImmutable());
        }
    }

}
