using static DiffMatchPatch.Operation;

namespace DiffMatchPatch;

public static class DiffList
{
    /// <summary>
    /// Compute and return the source text (all equalities and deletions).
    /// </summary>
    public static string Text1(this IEnumerable<Diff> diffs)
    {
        var sb = new StringBuilder();
        foreach (var diff in diffs)
        {
            if (diff.Operation != Insert)
                sb.Append(diff.TextSpan);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Compute and return the destination text (all equalities and insertions).
    /// </summary>
    public static string Text2(this IEnumerable<Diff> diffs)
    {
        var sb = new StringBuilder();
        foreach (var diff in diffs)
        {
            if (diff.Operation != Delete)
                sb.Append(diff.TextSpan);
        }
        return sb.ToString();
    }

    readonly record struct LevenshteinState(int Insertions, int Deletions, int Levenshtein)
    {
        public LevenshteinState Consolidate() => new(0, 0, Levenshtein + Math.Max(Insertions, Deletions));
    }

    /// <summary>
    /// Compute the Levenshtein distance; the number of inserted, deleted or substituted characters.
    /// </summary>
    internal static int Levenshtein(this IEnumerable<Diff> diffs)
    {
        var state = new LevenshteinState(0, 0, 0);
        foreach (var aDiff in diffs)
        {
            state = aDiff.Operation switch
            {
                Insert => state with { Insertions = state.Insertions + aDiff.TextLength },
                Delete => state with { Deletions = state.Deletions + aDiff.TextLength },
                Equal => state.Consolidate(),
                _ => throw new IndexOutOfRangeException()
            };
        }
        return state.Consolidate().Levenshtein;
    }

    private static StringBuilder AppendHtml(this StringBuilder sb, string tag, string backgroundColor, string content)
        => sb
        .Append(string.IsNullOrEmpty(backgroundColor) ? $"<{tag}>" : $"<{tag} style=\"background:{backgroundColor};\">")
        .Append(content)
        .Append($"</{tag}>");

    private static StringBuilder AppendHtml(this StringBuilder sb, Operation operation, string text) => operation switch
    {
        Insert => sb.AppendHtml("ins", "#e6ffe6", text),
        Delete => sb.AppendHtml("del", "#ffe6e6", text),
        Equal => sb.AppendHtml("span", "", text),
        _ => throw new IndexOutOfRangeException()
    };

    /// <summary>
    /// Convert a Diff list into a pretty HTML report.
    /// </summary>
    /// <param name="diffs"></param>
    /// <returns></returns>
    public static string PrettyHtml(this IEnumerable<Diff> diffs)
    {
        var sb = new StringBuilder();
        foreach (var diff in diffs)
            sb.AppendHtml(diff.Operation, diff.Text.HtmlEncodeLight());
        return sb.ToString();
    }

    private static string HtmlEncodeLight(this string s)
    {
        var text = new StringBuilder(s)
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\n", "&para;<br>")
            .ToString();
        return text;
    }

    static char ToDelta(this Operation o) => o switch
    {
        Delete => '-',
        Insert => '+',
        Equal => '=',
        _ => throw new ArgumentException($"Unknown Operation: {o}")
    };

    static Operation FromDelta(char c) => c switch
    {
        '-' => Delete,
        '+' => Insert,
        '=' => Equal,
        _ => throw new ArgumentException($"Invalid Delta Token: {c}")
    };

    /// <summary>
    /// Crush the diff into an encoded string which describes the operations
    /// required to transform text1 into text2.
    /// E.g. =3\t-2\t+ing  -> Keep 3 chars, delete 2 chars, insert 'ing'.
    /// Operations are tab-separated.  Inserted text is escaped using %xx
    /// notation.
    /// </summary>
    /// <param name="diffs"></param>
    /// <returns></returns>
    public static string ToDelta(this IEnumerable<Diff> diffs)
    {
        var sb = new StringBuilder();
        var first = true;
        foreach (var aDiff in diffs)
        {
            if (!first)
                sb.Append('\t');
            first = false;
            sb.Append(aDiff.Operation.ToDelta());
            if (aDiff.Operation == Insert)
                sb.AppendUrlEncoded(aDiff.TextSpan);
            else
                sb.Append(aDiff.TextLength);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Given the original text1, and an encoded string which describes the
    /// operations required to transform text1 into text2, compute the full diff.
    /// </summary>
    /// <param name="text1">Source string for the diff.</param>
    /// <param name="delta">Delta text.</param>
    /// <returns></returns>
    public static IEnumerable<Diff> FromDelta(string text1, string delta)
    {
        var pointer = 0;  // Cursor in text1

        foreach (var token in delta.SplitBy('\t'))
        {
            if (token.Length == 0)
            {
                // Blank tokens are ok (from a trailing \t).
                continue;
            }
            // Each token begins with a one character parameter which specifies the
            // operation of this token (delete, insert, equality).
            var param = token[1..];
            var operation = FromDelta(token[0]);
            int n = 0;
            if (operation != Insert)
            {
                if (!int.TryParse(param, out n))
                {
                    throw new ArgumentException($"Invalid number in Diff.FromDelta: {param}");
                }
                if (pointer > text1.Length - n)
                {
                    throw new ArgumentException($"Delta length ({pointer}) larger than source text length ({text1.Length}).");
                }
            }
            string text;
            (text, pointer) = operation switch
            {
                Insert => (param.Replace("+", "%2b").UrlDecoded(), pointer),
                Equal => (text1.AsSpan(pointer, n).ToString(), pointer + n),
                Delete => (text1.AsSpan(pointer, n).ToString(), pointer + n),
                _ => throw new ArgumentException($"Unknown Operation: {operation}")
            };
            yield return Diff.Create(operation, text);
        }
        if (pointer != text1.Length)
        {
            throw new ArgumentException($"Delta length ({pointer}) smaller than source text length ({text1.Length}).");
        }
    }

    /// <summary>
    /// Reorder and merge like edit sections.  Merge equalities.
    /// Any edit section can move as long as it doesn't cross an equality.
    /// </summary>
    internal static List<Diff> CleanupMerge(this IEnumerable<Diff> diffs)
    {
        using var engine = DiffEngine.FromDiffs(diffs);
        engine.CleanupMerge();
        return engine.ToList();
    }

    /// <summary>
    /// Look for single edits surrounded on both sides by equalities
    /// which can be shifted sideways to align the edit to a word boundary.
    /// e.g: The c<ins>at c</ins>ame. -> The <ins>cat </ins>came.
    /// </summary>
    internal static List<Diff> CleanupSemanticLossless(this IEnumerable<Diff> diffs)
    {
        using var engine = DiffEngine.FromDiffs(diffs);
        engine.CleanupSemanticLossless();
        return engine.ToList();
    }

    /// <summary>
    /// Reduce the number of edits by eliminating operationally trivial equalities.
    /// </summary>
    internal static IEnumerable<Diff> CleanupEfficiency(this IEnumerable<Diff> input, short diffEditCost = 4)
    {
        using var engine = DiffEngine.FromDiffs(input);
        return engine.CleanupEfficiency(diffEditCost) ? engine.ToList() : input;
    }

    /// <summary>
    /// A diff of two unrelated texts can be filled with coincidental matches. 
    /// For example, the diff of "mouse" and "sofas" is 
    /// `[(-1, "m"), (1, "s"), (0, "o"), (-1, "u"), (1, "fa"), (0, "s"), (-1, "e")]`. 
    /// While this is the optimum diff, it is difficult for humans to understand. Semantic 
    /// cleanup rewrites the diff, expanding it into a more intelligible format. The above 
    /// example would become: `[(-1, "mouse"), (1, "sofas")]`.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static IImmutableList<Diff> MakeHumanReadable(this IEnumerable<Diff> input)
    {
        using var engine = DiffEngine.FromDiffs(input);
        engine.CleanupSemantic();
        return engine.ToImmutableArray();
    }
    /// <summary>
    /// This function is similar to `OptimizeForReadability`, except that instead of optimising a diff 
    /// to be human-readable, it optimises the diff to be efficient for machine processing. The results 
    /// of both cleanup types are often the same.
    /// The efficiency cleanup is based on the observation that a diff made up of large numbers of 
    /// small diffs edits may take longer to process(in downstream applications) or take more capacity 
    /// to store or transmit than a smaller number of larger diffs.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="diffEditCost">The cost of handling a new edit in terms of handling extra characters in an existing edit. 
    /// The default value is 4, which means if expanding the length of a diff by three characters can eliminate one edit, 
    /// then that optimisation will reduce the total costs</param>
    /// <returns></returns>
    public static IImmutableList<Diff> OptimizeForMachineProcessing(this IEnumerable<Diff> input, short diffEditCost = 4)
    {
        using var engine = DiffEngine.FromDiffs(input);
        engine.CleanupEfficiency(diffEditCost);
        return engine.ToImmutableArray();
    }

    /// <summary>
    /// Reduce the number of edits by eliminating semantically trivial equalities.
    /// </summary>
    internal static List<Diff> CleanupSemantic(this IEnumerable<Diff> input)
    {
        using var engine = DiffEngine.FromDiffs(input);
        engine.CleanupSemantic();
        return engine.ToList();
    }

    /// <summary>
    /// Rehydrate the text in a diff from a string of line hashes to real lines of text.
    /// </summary>
    /// <param name="diffs"></param>
    /// <param name="lineArray">list of unique strings</param>
    /// <returns></returns>
    internal static IEnumerable<Diff> CharsToLines(this ICollection<Diff> diffs, IList<string> lineArray)
    {
        foreach (var diff in diffs)
        {
            var text = new StringBuilder();
            foreach (var c in diff.TextSpan)
            {
                text.Append(lineArray[c]);
            }
            yield return diff.Replace(text.ToString());
        }
    }

    /// <summary>
    /// Compute and return equivalent location in target text.
    /// </summary>
    internal static int FindEquivalentLocation2(this IEnumerable<Diff> diffs, int location1)
    {
        var chars1 = 0;
        var chars2 = 0;
        var lastChars1 = 0;
        var lastChars2 = 0;
        Diff lastDiff = Diff.Empty;
        foreach (var aDiff in diffs)
        {
            if (aDiff.Operation != Insert)
            {
                chars1 += aDiff.TextLength;
            }
            if (aDiff.Operation != Delete)
            {
                chars2 += aDiff.TextLength;
            }
            if (chars1 > location1)
            {
                lastDiff = aDiff;
                break;
            }
            lastChars1 = chars1;
            lastChars2 = chars2;
        }
        if (lastDiff.Operation == Delete)
        {
            // The location was deleted.
            return lastChars2;
        }
        // Add the remaining character length.
        return lastChars2 + (location1 - lastChars1);
    }


}
