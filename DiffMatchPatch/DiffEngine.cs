using System.Buffers;
using static DiffMatchPatch.Operation;

namespace DiffMatchPatch;

/// <summary>
/// One diff operation expressed as a range in the two texts being compared:
/// the text of an Equal or Delete is <c>text1[Start1..Start1+Length]</c>,
/// the text of an Insert is <c>text2[Start2..Start2+Length]</c>.
/// Start1/Start2 are the positions in text1/text2 just before this operation.
/// </summary>
internal readonly record struct DiffRange(Operation Op, int Start1, int Start2, int Length)
{
    public int End1 => Op == Insert ? Start1 : Start1 + Length;
    public int End2 => Op == Delete ? Start2 : Start2 + Length;
}

/// <summary>
/// Computes and cleans up diffs without materializing intermediate strings.
/// </summary>
/// <remarks>
/// A valid diff between two texts is fully determined by its operations and their lengths:
/// the text of every operation can be read back from the source texts. All algorithms here
/// (diff computation, merge, semantic and efficiency cleanup) therefore work on
/// <see cref="DiffRange"/> values that point into the two source texts. Every transformation
/// they apply keeps the diff valid, so strings only need to be created once, when the result
/// is converted to <see cref="Diff"/> objects.
/// </remarks>
internal sealed class DiffEngine : IDisposable
{
    private readonly string _text1;
    private readonly string _text2;
    private readonly bool _optimizeForSpeed;
    private readonly CancellationToken _token;
    private readonly RangeList _diffs = new();
    private readonly RangeList _scratch = new();
    private readonly List<int> _stack = new();

    public DiffEngine(string text1, string text2, bool optimizeForSpeed = false, CancellationToken token = default)
    {
        _text1 = text1;
        _text2 = text2;
        _optimizeForSpeed = optimizeForSpeed;
        _token = token;
    }

    internal RangeList Diffs => _diffs;

    /// <summary>
    /// Return the pooled buffers. The engine can not be used afterwards.
    /// </summary>
    public void Dispose()
    {
        _diffs.Dispose();
        _scratch.Dispose();
    }

    /// <summary>
    /// Load an existing list of diffs. The source texts are reconstructed from the diffs.
    /// </summary>
    public static DiffEngine FromDiffs(IEnumerable<Diff> diffs)
    {
        var list = diffs as IReadOnlyList<Diff> ?? diffs.ToList();
        if (diffs is DiffResult { SourceText: { } source, DestinationText: { } destination })
            return FromDiffs(list, source, destination);

        int length1 = 0, length2 = 0;
        foreach (var d in list)
        {
            if (d.Operation != Insert) length1 += d.TextLength;
            if (d.Operation != Delete) length2 += d.TextLength;
        }

        var text1 = new StringBuilder(length1);
        var text2 = new StringBuilder(length2);
        foreach (var d in list)
        {
            if (d.Operation != Insert) text1.Append(d.TextSpan);
            if (d.Operation != Delete) text2.Append(d.TextSpan);
        }

        return FromDiffs(list, text1.ToString(), text2.ToString());
    }

    private static DiffEngine FromDiffs(IReadOnlyList<Diff> list, string text1, string text2)
    {
        var engine = new DiffEngine(text1, text2);
        engine._diffs.EnsureCapacity(list.Count);
        int p1 = 0, p2 = 0;
        foreach (var d in list)
        {
            var range = new DiffRange(d.Operation, p1, p2, d.TextLength);
            engine._diffs.Add(range);
            p1 = range.End1;
            p2 = range.End2;
        }
        return engine;
    }

    private ReadOnlySpan<char> Text(DiffRange d)
        => d.Op == Insert ? _text2.AsSpan(d.Start2, d.Length) : _text1.AsSpan(d.Start1, d.Length);

    private Diff ToDiff(DiffRange d)
    {
        var source = d.Op == Insert ? _text2 : _text1;
        var start = d.Op == Insert ? d.Start2 : d.Start1;
        return Diff.Create(d.Op, d.Length == 0 ? string.Empty : source.Substring(start, d.Length));
    }

    public List<Diff> ToList()
    {
        var result = new List<Diff>(_diffs.Count);
        foreach (var d in _diffs.AsSpan()) result.Add(ToDiff(d));
        return result;
    }

    public ImmutableArray<Diff> ToImmutableArray()
    {
        var builder = ImmutableArray.CreateBuilder<Diff>(_diffs.Count);
        foreach (var d in _diffs.AsSpan()) builder.Add(ToDiff(d));
        return builder.MoveToImmutable();
    }

    private void Add(Operation op, int start1, int start2, int length)
    {
        if (length > 0) _diffs.Add(new DiffRange(op, start1, start2, length));
    }

    #region Diff computation

    /// <summary>
    /// Find the differences between the two texts.
    /// </summary>
    public void Compute(bool checklines) => Compute(0, _text1.Length, 0, _text2.Length, checklines);

    /// <summary>
    /// Find the differences between text1[s1..s1+n1] and text2[s2..s2+n2], appending them to the diff list.
    /// Simplifies the problem by stripping any common prefix or suffix off the texts before diffing.
    /// </summary>
    public void Compute(int s1, int n1, int s2, int n2, bool checklines)
    {
        if (n1 == 0 && n2 == 0)
            return;

        var start = _diffs.Count;
        var prefix = TextUtil.CommonPrefix(_text1.AsSpan(s1, n1), _text2.AsSpan(s2, n2));
        if (prefix == n1 && prefix == n2)
        {
            Add(Equal, s1, s2, n1);
            return;
        }

        // Trim off common prefix (speedup).
        Add(Equal, s1, s2, prefix);
        s1 += prefix; n1 -= prefix;
        s2 += prefix; n2 -= prefix;

        // Trim off common suffix (speedup).
        var suffix = TextUtil.CommonSuffix(_text1.AsSpan(s1, n1), _text2.AsSpan(s2, n2));
        n1 -= suffix;
        n2 -= suffix;

        // Compute the diff on the middle block.
        ComputeImpl(s1, n1, s2, n2, checklines);

        Add(Equal, s1 + n1, s2 + n2, suffix);

        CleanupMerge(start);
    }

    /// <summary>
    /// Find the differences between two texts. Assumes that the texts do not
    /// have any common prefix or suffix.
    /// </summary>
    private void ComputeImpl(int s1, int n1, int s2, int n2, bool checklines)
    {
        if (n1 == 0)
        {
            // Just add some text (speedup).
            Add(Insert, s1, s2, n2);
            return;
        }

        if (n2 == 0)
        {
            // Just delete some text (speedup).
            Add(Delete, s1, s2, n1);
            return;
        }

        if (n1 > n2)
        {
            // Shorter text is inside the longer text (speedup).
            var i = _text1.AsSpan(s1, n1).IndexOf(_text2.AsSpan(s2, n2), StringComparison.Ordinal);
            if (i != -1)
            {
                Add(Delete, s1, s2, i);
                Add(Equal, s1 + i, s2, n2);
                Add(Delete, s1 + i + n2, s2 + n2, n1 - i - n2);
                return;
            }
        }
        else
        {
            var i = _text2.AsSpan(s2, n2).IndexOf(_text1.AsSpan(s1, n1), StringComparison.Ordinal);
            if (i != -1)
            {
                Add(Insert, s1, s2, i);
                Add(Equal, s1, s2 + i, n1);
                Add(Insert, s1 + n1, s2 + i + n1, n2 - i - n1);
                return;
            }
        }

        if (Math.Min(n1, n2) == 1)
        {
            // Single character string.
            // After the previous speedup, the character can't be an equality.
            Add(Delete, s1, s2, n1);
            Add(Insert, s1 + n1, s2, n2);
            return;
        }

        // Don't risk returning a non-optimal diff if we have unlimited time.
        if (_optimizeForSpeed)
        {
            // Check to see if the problem can be split in two.
            var hm = TextUtil.HalfMatch(new StringSlice(_text1, s1, n1), new StringSlice(_text2, s2, n2));
            if (!hm.IsEmpty)
            {
                // A half-match was found. Send both pairs off for separate processing.
                Compute(hm.Prefix1.Offset, hm.Prefix1.Length, hm.Prefix2.Offset, hm.Prefix2.Length, checklines);
                Add(Equal, hm.Prefix1.Offset + hm.Prefix1.Length, hm.Prefix2.Offset + hm.Prefix2.Length, hm.CommonMiddle.Length);
                Compute(hm.Suffix1.Offset, hm.Suffix1.Length, hm.Suffix2.Offset, hm.Suffix2.Length, checklines);
                return;
            }
        }

        if (checklines && n1 > 100 && n2 > 100)
        {
            LineDiff(s1, n1, s2, n2);
            return;
        }

        Bisect(s1, n1, s2, n2);
    }

    /// <summary>
    /// Do a quick line-level diff on both strings, then rediff the parts for
    /// greater accuracy. This speedup can produce non-minimal diffs.
    /// </summary>
    private void LineDiff(int s1, int n1, int s2, int n2)
    {
        // Scan the text on a line-by-line basis first.
        var compressor = new LineToCharCompressor();
        var chars1 = compressor.Compress(_text1, s1, n1, char.MaxValue * 2 / 3);
        var chars2 = compressor.Compress(_text2, s2, n2, char.MaxValue);
        using var lineEngine = new DiffEngine(chars1, chars2, _optimizeForSpeed, _token);
        lineEngine.Compute(false);

        // Map the line-level diff back onto the original text.
        var start = _diffs.Count;
        int p1 = s1, p2 = s2;
        foreach (var d in lineEngine._diffs.AsSpan())
        {
            var length = 0;
            foreach (var c in lineEngine.Text(d))
                length += compressor.LineLength(c);
            var range = new DiffRange(d.Op, p1, p2, length);
            _diffs.Add(range);
            p1 = range.End1;
            p2 = range.End2;
        }

        // Eliminate freak matches (e.g. blank lines)
        CleanupSemantic(start);

        // Rediff any replacement blocks, this time character-by-character.
        using var lineDiffs = new RangeList();
        lineDiffs.AddRange(_diffs.AsSpan(start));
        _diffs.Truncate(start);
        int deleteLength = 0, insertLength = 0, runStart1 = 0, runStart2 = 0;
        for (var i = 0; i <= lineDiffs.Count; i++)
        {
            var d = i < lineDiffs.Count ? lineDiffs[i] : new DiffRange(Equal, 0, 0, 0);
            if (d.Op != Equal)
            {
                if (deleteLength == 0 && insertLength == 0)
                {
                    runStart1 = d.Start1;
                    runStart2 = d.Start2;
                }
                if (d.Op == Delete) deleteLength += d.Length;
                else insertLength += d.Length;
                continue;
            }

            if (deleteLength > 0 && insertLength > 0)
                Compute(runStart1, deleteLength, runStart2, insertLength, false);
            else if (deleteLength > 0)
                Add(Delete, runStart1, runStart2, deleteLength);
            else if (insertLength > 0)
                Add(Insert, runStart1, runStart2, insertLength);

            Add(Equal, d.Start1, d.Start2, d.Length);
            deleteLength = insertLength = 0;
        }
    }

    /// <summary>
    /// Find the 'middle snake' of a diff, split the problem in two and recursively construct the diff.
    /// See Myers 1986 paper: An O(ND) Difference Algorithm and Its Variations.
    /// </summary>
    public void Bisect(int s1, int n1, int s2, int n2)
    {
        var (x, y) = FindMiddleSnake(_text1.AsSpan(s1, n1), _text2.AsSpan(s2, n2), _token);
        if (x >= 0)
        {
            // Compute both diffs serially.
            Compute(s1, x, s2, y, false);
            Compute(s1 + x, n1 - x, s2 + y, n2 - y, false);
        }
        else
        {
            // Diff took too long and hit the deadline or
            // number of diffs equals number of characters, no commonality at all.
            Add(Delete, s1, s2, n1);
            Add(Insert, s1 + n1, s2, n2);
        }
    }

    /// <summary>
    /// Find the 'middle snake' of a diff. Returns (-1, -1) if there is none
    /// (no commonality at all, or cancelled).
    /// </summary>
    private static (int x, int y) FindMiddleSnake(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2, CancellationToken token)
    {
        var text1Length = text1.Length;
        var text2Length = text2.Length;
        var maxD = (text1Length + text2Length + 1) / 2;
        var vOffset = maxD;
        var vLength = 2 * maxD;
        var pool = ArrayPool<int>.Shared;
        var v1 = pool.Rent(vLength);
        var v2 = pool.Rent(vLength);
        try
        {
            v1.AsSpan(0, vLength).Fill(-1);
            v2.AsSpan(0, vLength).Fill(-1);
            v1[vOffset + 1] = 0;
            v2[vOffset + 1] = 0;
            var delta = text1Length - text2Length;
            // If the total number of characters is odd, then the front path will
            // collide with the reverse path.
            var front = delta % 2 != 0;
            // Offsets for start and end of k loop.
            // Prevents mapping of space beyond the grid.
            var k1Start = 0;
            var k1End = 0;
            var k2Start = 0;
            var k2End = 0;
            for (var d = 0; d < maxD; d++)
            {
                // Bail out if cancelled.
                if (token.IsCancellationRequested)
                {
                    break;
                }

                // Walk the front path one step.
                for (var k1 = -d + k1Start; k1 <= d - k1End; k1 += 2)
                {
                    var k1Offset = vOffset + k1;
                    int x1;
                    if (k1 == -d || k1 != d && v1[k1Offset - 1] < v1[k1Offset + 1])
                    {
                        x1 = v1[k1Offset + 1];
                    }
                    else
                    {
                        x1 = v1[k1Offset - 1] + 1;
                    }
                    var y1 = x1 - k1;
                    while (x1 < text1Length && y1 < text2Length && text1[x1] == text2[y1])
                    {
                        x1++;
                        y1++;
                    }
                    v1[k1Offset] = x1;
                    if (x1 > text1Length)
                    {
                        // Ran off the right of the graph.
                        k1End += 2;
                    }
                    else if (y1 > text2Length)
                    {
                        // Ran off the bottom of the graph.
                        k1Start += 2;
                    }
                    else if (front)
                    {
                        var k2Offset = vOffset + delta - k1;
                        if (k2Offset >= 0 && k2Offset < vLength && v2[k2Offset] != -1)
                        {
                            // Mirror x2 onto top-left coordinate system.
                            var x2 = text1Length - v2[k2Offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return (x1, y1);
                            }
                        }
                    }
                }

                // Walk the reverse path one step.
                for (var k2 = -d + k2Start; k2 <= d - k2End; k2 += 2)
                {
                    var k2Offset = vOffset + k2;
                    int x2;
                    if (k2 == -d || k2 != d && v2[k2Offset - 1] < v2[k2Offset + 1])
                    {
                        x2 = v2[k2Offset + 1];
                    }
                    else
                    {
                        x2 = v2[k2Offset - 1] + 1;
                    }
                    var y2 = x2 - k2;
                    while (x2 < text1Length && y2 < text2Length
                           && text1[text1Length - x2 - 1] == text2[text2Length - y2 - 1])
                    {
                        x2++;
                        y2++;
                    }
                    v2[k2Offset] = x2;
                    if (x2 > text1Length)
                    {
                        // Ran off the left of the graph.
                        k2End += 2;
                    }
                    else if (y2 > text2Length)
                    {
                        // Ran off the top of the graph.
                        k2Start += 2;
                    }
                    else if (!front)
                    {
                        var k1Offset = vOffset + delta - k2;
                        if (k1Offset >= 0 && k1Offset < vLength && v1[k1Offset] != -1)
                        {
                            var x1 = v1[k1Offset];
                            var y1 = vOffset + x1 - k1Offset;
                            // Mirror x2 onto top-left coordinate system.
                            x2 = text1Length - v2[k2Offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return (x1, y1);
                            }
                        }
                    }
                }
            }
            return (-1, -1);
        }
        finally
        {
            pool.Return(v1);
            pool.Return(v2);
        }
    }

    #endregion

    #region Merge

    /// <summary>
    /// Reorder and merge like edit sections, merge equalities (for the diffs starting at index <paramref name="start"/>).
    /// Any edit section can move as long as it doesn't cross an equality.
    /// </summary>
    public void CleanupMerge(int start = 0)
    {
        bool changes;
        do
        {
            MergePass1(start);
            changes = MergePass2(start);
        } while (changes);
    }

    /// <summary>
    /// Merge runs of inserts/deletes and equalities, factoring out common prefixes and suffixes of edit runs.
    /// </summary>
    private void MergePass1(int start)
    {
        var output = _scratch;
        output.Clear();
        int deleteLength = 0, insertLength = 0, run1 = 0, run2 = 0;
        var inRun = false;
        var count = _diffs.Count;
        for (var i = start; i <= count; i++)
        {
            var end = i == count;
            var d = end ? default : _diffs[i];
            if (!end && d.Op != Equal)
            {
                if (!inRun)
                {
                    inRun = true;
                    run1 = d.Start1;
                    run2 = d.Start2;
                }
                if (d.Op == Delete) deleteLength += d.Length;
                else insertLength += d.Length;
                continue;
            }

            var equalLength = end ? 0 : d.Length;
            if (deleteLength > 0 || insertLength > 0)
            {
                // The deleted text is text1[run1..], the inserted text is text2[run2..]
                // Factor out any common prefix.
                var prefix = TextUtil.CommonPrefix(_text1.AsSpan(run1, deleteLength), _text2.AsSpan(run2, insertLength));
                AppendEqual(output, run1, run2, prefix);
                run1 += prefix; run2 += prefix;
                deleteLength -= prefix; insertLength -= prefix;

                // Factor out any common suffix; it becomes part of the next equality.
                var suffix = TextUtil.CommonSuffix(_text1.AsSpan(run1, deleteLength), _text2.AsSpan(run2, insertLength));
                deleteLength -= suffix; insertLength -= suffix;

                if (deleteLength > 0) output.Add(new DiffRange(Delete, run1, run2, deleteLength));
                if (insertLength > 0) output.Add(new DiffRange(Insert, run1 + deleteLength, run2, insertLength));
                AppendEqual(output, run1 + deleteLength, run2 + insertLength, suffix + equalLength);
            }
            else if (!end)
            {
                AppendEqual(output, d.Start1, d.Start2, equalLength);
            }

            inRun = false;
            deleteLength = insertLength = 0;
        }

        _diffs.Truncate(start);
        _diffs.AddRange(output.AsSpan());
    }

    private static void AppendEqual(RangeList output, int start1, int start2, int length)
    {
        if (length == 0) return;
        var last = output.Count - 1;
        if (last >= 0 && output[last].Op == Equal)
            output[last] = output[last] with { Length = output[last].Length + length };
        else
            output.Add(new DiffRange(Equal, start1, start2, length));
    }

    /// <summary>
    /// Look for single edits surrounded on both sides by equalities
    /// which can be shifted sideways to eliminate an equality.
    /// e.g: A&lt;ins&gt;BA&lt;/ins&gt;C -> &lt;ins&gt;AB&lt;/ins&gt;AC
    /// </summary>
    private bool MergePass2(int start)
    {
        var changes = false;
        // Intentionally ignore the first and last element (don't need checking).
        for (var i = start + 1; i < _diffs.Count - 1; i++)
        {
            var previous = _diffs[i - 1];
            var current = _diffs[i];
            var next = _diffs[i + 1];
            if (previous.Op != Equal || next.Op != Equal)
                continue;

            var currentText = Text(current);
            var previousText = Text(previous);
            var nextText = Text(next);
            if (currentText.Length >= previousText.Length && currentText[(currentText.Length - previousText.Length)..].SequenceEqual(previousText))
            {
                // Shift the edit over the previous equality.
                var shifted = current with { Start1 = previous.Start1, Start2 = previous.Start2 };
                _diffs[i] = shifted;
                _diffs[i + 1] = new DiffRange(Equal, shifted.End1, shifted.End2, previous.Length + next.Length);
                _diffs.RemoveAt(i - 1);
                changes = true;
            }
            else if (currentText.Length >= nextText.Length && currentText[..nextText.Length].SequenceEqual(nextText))
            {
                // Shift the edit over the next equality.
                var merged = previous with { Length = previous.Length + next.Length };
                _diffs[i - 1] = merged;
                _diffs[i] = current with { Start1 = merged.End1, Start2 = merged.End2 };
                _diffs.RemoveAt(i + 1);
                changes = true;
            }
        }
        return changes;
    }

    #endregion

    #region Semantic cleanup

    /// <summary>
    /// Reduce the number of edits by eliminating semantically trivial equalities.
    /// </summary>
    public void CleanupSemantic(int start = 0)
    {
        var equalities = _stack;
        equalities.Clear();
        // Length of the last equality, -1 if none.
        var lastEqualityLength = -1;
        var pointer = start;
        // Number of characters that changed prior to the equality.
        var lengthInsertions1 = 0;
        var lengthDeletions1 = 0;
        // Number of characters that changed after the equality.
        var lengthInsertions2 = 0;
        var lengthDeletions2 = 0;
        while (pointer < _diffs.Count)
        {
            var d = _diffs[pointer];
            if (d.Op == Equal)
            {
                equalities.Add(pointer);
                lengthInsertions1 = lengthInsertions2;
                lengthDeletions1 = lengthDeletions2;
                lengthInsertions2 = 0;
                lengthDeletions2 = 0;
                lastEqualityLength = d.Length;
            }
            else
            {
                if (d.Op == Insert)
                    lengthInsertions2 += d.Length;
                else
                    lengthDeletions2 += d.Length;

                // Eliminate an equality that is smaller or equal to the edits on both sides of it.
                if (lastEqualityLength >= 0
                    && lastEqualityLength <= Math.Max(lengthInsertions1, lengthDeletions1)
                    && lastEqualityLength <= Math.Max(lengthInsertions2, lengthDeletions2))
                {
                    SplitEquality(equalities[equalities.Count - 1]);
                    // Throw away the equality we just deleted, and the previous one.
                    equalities.RemoveAt(equalities.Count - 1);
                    if (equalities.Count > 0)
                        equalities.RemoveAt(equalities.Count - 1);
                    pointer = equalities.Count > 0 ? equalities[equalities.Count - 1] : start - 1;
                    lengthInsertions1 = 0;
                    lengthDeletions1 = 0;
                    lengthInsertions2 = 0;
                    lengthDeletions2 = 0;
                    lastEqualityLength = -1;
                }
            }
            pointer++;
        }

        CleanupMerge(start);
        CleanupSemanticLossless(start);
        EliminateOverlaps(start);
    }

    /// <summary>
    /// Replace the equality at <paramref name="index"/> by a delete and an insert of the same text.
    /// </summary>
    private void SplitEquality(int index)
    {
        var equality = _diffs[index];
        _diffs[index] = equality with { Op = Delete };
        _diffs.Insert(index + 1, new DiffRange(Insert, equality.Start1 + equality.Length, equality.Start2, equality.Length));
    }

    /// <summary>
    /// Find any overlaps between deletions and insertions.
    /// e.g: &lt;del&gt;abcxxx&lt;/del&gt;&lt;ins&gt;xxxdef&lt;/ins&gt; -> &lt;del&gt;abc&lt;/del&gt;xxx&lt;ins&gt;def&lt;/ins&gt;
    /// e.g: &lt;del&gt;xxxabc&lt;/del&gt;&lt;ins&gt;defxxx&lt;/ins&gt; -> &lt;ins&gt;def&lt;/ins&gt;xxx&lt;del&gt;abc&lt;/del&gt;
    /// Only extract an overlap if it is as big as the edit ahead or behind it.
    /// </summary>
    private void EliminateOverlaps(int start)
    {
        var pointer = start + 1;
        while (pointer < _diffs.Count)
        {
            var deletion = _diffs[pointer - 1];
            var insertion = _diffs[pointer];
            if (deletion.Op == Delete && insertion.Op == Insert)
            {
                var deleted = Text(deletion);
                var inserted = Text(insertion);
                var overlapLength1 = TextUtil.CommonOverlap(deleted, inserted);
                var overlapLength2 = TextUtil.CommonOverlap(inserted, deleted);
                var minLength = Math.Min(deleted.Length, inserted.Length);
                int b1 = deletion.Start1, b2 = deletion.Start2;
                int dl = deletion.Length, il = insertion.Length;

                if (overlapLength1 >= overlapLength2 && overlapLength1 >= minLength / 2.0)
                {
                    // Overlap found. Insert an equality and trim the surrounding edits.
                    _diffs[pointer - 1] = new DiffRange(Delete, b1, b2, dl - overlapLength1);
                    _diffs[pointer] = new DiffRange(Equal, b1 + dl - overlapLength1, b2, overlapLength1);
                    _diffs.Insert(pointer + 1, new DiffRange(Insert, b1 + dl, b2 + overlapLength1, il - overlapLength1));
                    pointer++;
                }
                else if (overlapLength2 >= overlapLength1 && overlapLength2 >= minLength / 2.0)
                {
                    // Reverse overlap found. Insert an equality and swap and trim the surrounding edits.
                    _diffs[pointer - 1] = new DiffRange(Insert, b1, b2, il - overlapLength2);
                    _diffs[pointer] = new DiffRange(Equal, b1, b2 + il - overlapLength2, overlapLength2);
                    _diffs.Insert(pointer + 1, new DiffRange(Delete, b1 + overlapLength2, b2 + il, dl - overlapLength2));
                    pointer++;
                }
                pointer++;
            }
            pointer++;
        }
    }

    /// <summary>
    /// Look for single edits surrounded on both sides by equalities
    /// which can be shifted sideways to align the edit to a word boundary.
    /// e.g: The c&lt;ins&gt;at c&lt;/ins&gt;ame. -> The &lt;ins&gt;cat &lt;/ins&gt;came.
    /// </summary>
    public void CleanupSemanticLossless(int start = 0)
    {
        // Intentionally ignore the first and last element (don't need checking).
        var pointer = start + 1;
        while (pointer < _diffs.Count - 1)
        {
            var previous = _diffs[pointer - 1];
            var current = _diffs[pointer];
            var next = _diffs[pointer + 1];
            if (previous.Op == Equal && next.Op == Equal)
            {
                // This is a single edit surrounded by equalities. The block is
                // equality1 + edit + equality2; only the length of equality1 varies.
                var block = new EditBlock(_text1, _text2, current.Op, previous.Start1, previous.Start2, current.Length, previous.Length + next.Length);
                var equality1 = previous.Length;

                // First, shift the edit as far left as possible.
                equality1 -= TextUtil.CommonSuffix(block.Equality1(equality1), block.Edit(equality1));

                // Second, step character by character right, looking for the best fit.
                // The >= encourages trailing rather than leading whitespace on edits.
                var best = equality1;
                var bestScore = block.Score(equality1);
                while (true)
                {
                    var edit = block.Edit(equality1);
                    var equality2 = block.Equality2(equality1);
                    if (edit.Length == 0 || equality2.Length == 0 || edit[0] != equality2[0])
                        break;
                    equality1++;
                    var score = block.Score(equality1);
                    if (score >= bestScore)
                    {
                        bestScore = score;
                        best = equality1;
                    }
                }

                if (previous.Length != best)
                {
                    // We have an improvement, save it back to the diff.
                    if (best != 0)
                    {
                        _diffs[pointer - 1] = previous with { Length = best };
                    }
                    else
                    {
                        _diffs.RemoveAt(pointer - 1);
                        pointer--;
                    }
                    var shifted = current with { Start1 = previous.Start1 + best, Start2 = previous.Start2 + best };
                    _diffs[pointer] = shifted;
                    var equality2Length = block.EqualityLength - best;
                    if (equality2Length != 0)
                    {
                        _diffs[pointer + 1] = new DiffRange(Equal, shifted.End1, shifted.End2, equality2Length);
                    }
                    else
                    {
                        _diffs.RemoveAt(pointer + 1);
                        pointer--;
                    }
                }
            }
            pointer++;
        }
    }

    /// <summary>
    /// An edit between two equalities, as positions in the source texts.
    /// The texts of the equalities and the edit are read for a given length of the first equality.
    /// </summary>
    private readonly ref struct EditBlock
    {
        private readonly string _text1;
        private readonly string _text2;
        private readonly Operation _op;
        private readonly int _start1;
        private readonly int _start2;
        private readonly int _editLength;
        public readonly int EqualityLength;

        public EditBlock(string text1, string text2, Operation op, int start1, int start2, int editLength, int equalityLength)
        {
            _text1 = text1;
            _text2 = text2;
            _op = op;
            _start1 = start1;
            _start2 = start2;
            _editLength = editLength;
            EqualityLength = equalityLength;
        }

        public ReadOnlySpan<char> Equality1(int length) => _text1.AsSpan(_start1, length);

        public ReadOnlySpan<char> Edit(int equality1) => _op == Insert
            ? _text2.AsSpan(_start2 + equality1, _editLength)
            : _text1.AsSpan(_start1 + equality1, _editLength);

        public ReadOnlySpan<char> Equality2(int equality1) => _op == Insert
            ? _text1.AsSpan(_start1 + equality1, EqualityLength - equality1)
            : _text1.AsSpan(_start1 + equality1 + _editLength, EqualityLength - equality1);

        public int Score(int equality1)
        {
            var edit = Edit(equality1);
            return SemanticScore(Equality1(equality1), edit) + SemanticScore(edit, Equality2(equality1));
        }
    }

    /// <summary>
    /// Given two strings, compute a score representing whether the internal
    /// boundary falls on logical boundaries. Scores range from 6 (best) to 0 (worst).
    /// </summary>
    private static int SemanticScore(ReadOnlySpan<char> one, ReadOnlySpan<char> two)
    {
        if (one.Length == 0 || two.Length == 0)
        {
            // Edges are the best.
            return 6;
        }

        // Each port of this function behaves slightly differently due to
        // subtle differences in each language's definition of things like
        // 'whitespace'.  Since this function's purpose is largely cosmetic,
        // the choice has been made to use each language's native features
        // rather than force total conformity.
        var char1 = one[one.Length - 1];
        var char2 = two[0];
        var nonAlphaNumeric1 = !char.IsLetterOrDigit(char1);
        var nonAlphaNumeric2 = !char.IsLetterOrDigit(char2);
        var whitespace1 = nonAlphaNumeric1 && char.IsWhiteSpace(char1);
        var whitespace2 = nonAlphaNumeric2 && char.IsWhiteSpace(char2);
        var lineBreak1 = whitespace1 && char.IsControl(char1);
        var lineBreak2 = whitespace2 && char.IsControl(char2);
        var blankLine1 = lineBreak1 && EndsWithBlankLine(one);
        var blankLine2 = lineBreak2 && StartsWithBlankLine(two);

        if (blankLine1 || blankLine2)
            // Five points for blank lines.
            return 5;
        if (lineBreak1 || lineBreak2)
            // Four points for line breaks.
            return 4;
        if (nonAlphaNumeric1 && !whitespace1 && whitespace2)
            // Three points for end of sentences.
            return 3;
        if (whitespace1 || whitespace2)
            // Two points for whitespace.
            return 2;
        if (nonAlphaNumeric1 || nonAlphaNumeric2)
            // One point for non-alphanumeric.
            return 1;
        return 0;
    }

    // Equivalent to the regex "\n\r?\n\Z" (\Z also matches before a final \n, which is already covered).
    private static bool EndsWithBlankLine(ReadOnlySpan<char> s)
        => s.EndsWith("\n\n".AsSpan()) || s.EndsWith("\n\r\n".AsSpan());

    // Equivalent to the regex "\A\r?\n\r?\n".
    private static bool StartsWithBlankLine(ReadOnlySpan<char> s)
        => s.StartsWith("\n\n".AsSpan()) || s.StartsWith("\n\r\n".AsSpan())
        || s.StartsWith("\r\n\n".AsSpan()) || s.StartsWith("\r\n\r\n".AsSpan());

    #endregion

    #region Efficiency cleanup

    /// <summary>
    /// Reduce the number of edits by eliminating operationally trivial equalities.
    /// </summary>
    /// <returns>true if the diffs were changed</returns>
    public bool CleanupEfficiency(short diffEditCost, int start = 0)
    {
        var changes = false;
        var equalities = _stack;
        equalities.Clear();
        // Length of the last candidate equality (0 if none).
        var lastEqualityLength = 0;
        // Is there an insertion/deletion operation before the last equality.
        var insertionBefore = false;
        var deletionBefore = false;
        // Is there an insertion/deletion operation after the last equality.
        var insertionAfter = false;
        var deletionAfter = false;

        for (var i = start; i < _diffs.Count; i++)
        {
            var d = _diffs[i];
            if (d.Op == Equal)
            {
                if (d.Length < diffEditCost && (insertionAfter || deletionAfter))
                {
                    // Candidate found.
                    equalities.Add(i);
                    insertionBefore = insertionAfter;
                    deletionBefore = deletionAfter;
                    lastEqualityLength = d.Length;
                }
                else
                {
                    // Not a candidate, and can never become one.
                    equalities.Clear();
                    lastEqualityLength = 0;
                }
                insertionAfter = deletionAfter = false;
            }
            else
            {
                if (d.Op == Delete)
                    deletionAfter = true;
                else
                    insertionAfter = true;

                /*
                 * Five types to be split:
                 * <ins>A</ins><del>B</del>XY<ins>C</ins><del>D</del>
                 * <ins>A</ins>X<ins>C</ins><del>D</del>
                 * <ins>A</ins><del>B</del>X<ins>C</ins>
                 * <ins>A</del>X<ins>C</ins><del>D</del>
                 * <ins>A</ins><del>B</del>X<del>C</del>
                 */
                var count = (insertionBefore ? 1 : 0) + (deletionBefore ? 1 : 0) + (insertionAfter ? 1 : 0) + (deletionAfter ? 1 : 0);
                if (lastEqualityLength != 0
                    && ((insertionBefore && deletionBefore && insertionAfter && deletionAfter)
                        || (lastEqualityLength < diffEditCost / 2 && count == 3)))
                {
                    SplitEquality(equalities[equalities.Count - 1]);
                    // Throw away the equality we just deleted.
                    equalities.RemoveAt(equalities.Count - 1);
                    lastEqualityLength = 0;
                    if (insertionBefore && deletionBefore)
                    {
                        // No changes made which could affect previous entry, keep going.
                        insertionAfter = deletionAfter = true;
                        equalities.Clear();
                    }
                    else
                    {
                        if (equalities.Count > 0)
                            equalities.RemoveAt(equalities.Count - 1);
                        i = equalities.Count > 0 ? equalities[equalities.Count - 1] : start - 1;
                        insertionAfter = deletionAfter = false;
                    }
                    changes = true;
                }
            }
        }

        if (changes)
            CleanupMerge(start);
        return changes;
    }

    #endregion
}
