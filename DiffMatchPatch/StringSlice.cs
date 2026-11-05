namespace DiffMatchPatch;

/// <summary>
/// A lightweight view over a portion of a string. Unlike <see cref="ReadOnlySpan{T}"/>
/// it can be stored in fields and records, so the diff algorithm can pass sub-ranges
/// around without allocating intermediate strings.
/// </summary>
internal readonly struct StringSlice : IEquatable<StringSlice>
{
    public readonly string Source;
    public readonly int Offset;
    public readonly int Length;

    public StringSlice(string source) : this(source, 0, source.Length) { }

    public StringSlice(string source, int offset, int length)
    {
        Source = source;
        Offset = offset;
        Length = length;
    }

    public bool Equals(StringSlice other) => Span.SequenceEqual(other.Span);
    public override bool Equals(object obj) => obj is StringSlice other && Equals(other);
    public override int GetHashCode()
    {
        return TextUtil.OrdinalHash(Span);
    }
    public static bool operator ==(StringSlice left, StringSlice right) => left.Equals(right);
    public static bool operator !=(StringSlice left, StringSlice right) => !left.Equals(right);

    public ReadOnlySpan<char> Span => Source.AsSpan(Offset, Length);

    public char this[int index] => Source[Offset + index];

    public bool IsEmpty => Length == 0;

    /// <summary>Equivalent to span.Slice(start, length).</summary>
    public StringSlice Slice(int start, int length) => new(Source, Offset + start, length);

    /// <summary>Equivalent to span[start..].</summary>
    public StringSlice Slice(int start) => new(Source, Offset + start, Length - start);

    public override string ToString() => Length == 0 ? string.Empty : Source.Substring(Offset, Length);

    /// <summary>Implicit conversion from string to StringSlice (wraps the entire string).</summary>
    public static implicit operator StringSlice(string source) => new(source);

}
