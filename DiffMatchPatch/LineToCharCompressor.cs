using System.Buffers;

namespace DiffMatchPatch;

/// <summary>
/// Maps every distinct line of text to a single character, so that a line-level diff
/// can be computed with the character diff algorithm.
/// </summary>
/// <remarks>
/// Lines are stored as ranges of their source text (no substring per line is allocated).
/// </remarks>
class LineToCharCompressor
{
    private readonly struct Line
    {
        public readonly string Source;
        public readonly int Start;
        public readonly int Length;

        public Line(string source, int start, int length)
        {
            Source = source;
            Start = start;
            Length = length;
        }

        public ReadOnlySpan<char> Span => Source.AsSpan(Start, Length);
    }

    private sealed class LineComparer : IEqualityComparer<Line>
    {
        public static readonly LineComparer Instance = new();
        public bool Equals(Line x, Line y) => x.Span.SequenceEqual(y.Span);
        public int GetHashCode(Line line) => TextUtil.OrdinalHash(line.Span);
    }

    // e.g. _lines[4] == "Hello\n"
    // e.g. _lineHash["Hello\n"] == 4
    private readonly List<Line> _lines = new();
    private readonly Dictionary<Line, char> _lineHash = new(LineComparer.Instance);

    /// <summary>
    /// Compresses all lines of a text to a series of indexes (starting at \u0000)
    /// </summary>
    public string Compress(string text, int maxLines = char.MaxValue) => Compress(text, 0, text.Length, maxLines);

    /// <summary>
    /// Compresses all lines of text[start..start+length] to a series of indexes.
    /// Once <paramref name="maxLines"/> distinct lines have been seen, the rest of the text is treated as one line.
    /// </summary>
    public string Compress(string text, int start, int length, int maxLines = char.MaxValue)
    {
        if (length == 0) return string.Empty;
        var buffer = ArrayPool<char>.Shared.Rent(length);
        try
        {
            var count = 0;
            var end = start + length;
            var position = start;
            while (position < end)
            {
                var i = text.AsSpan(position, end - position).IndexOf('\n');
                var lineEnd = _lines.Count == maxLines || i == -1 ? end : position + i + 1;
                var line = new Line(text, position, lineEnd - position);
                if (!_lineHash.TryGetValue(line, out var c))
                {
                    c = (char)_lines.Count;
                    _lines.Add(line);
                    _lineHash.Add(line, c);
                }
                buffer[count++] = c;
                position = lineEnd;
            }
            return new string(buffer, 0, count);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Decompresses a series of characters that was previously compressed back to the original lines of text.
    /// </summary>
    public string Decompress(string text)
    {
        var sb = new StringBuilder();
        foreach (var c in text)
            sb.Append(_lines[c].Span);
        if (text.Length == char.MaxValue)
            sb.Append(_lines[char.MaxValue].Span);
        return sb.ToString();
    }

    /// <summary>
    /// The length of the line represented by <paramref name="c"/>.
    /// </summary>
    public int LineLength(char c) => _lines[c].Length;
}
