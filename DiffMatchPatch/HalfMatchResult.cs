namespace DiffMatchPatch;

internal readonly record struct HalfMatchResult(StringSlice Prefix1, StringSlice Suffix1, StringSlice Prefix2, StringSlice Suffix2, StringSlice CommonMiddle)
{
    public bool IsEmpty => CommonMiddle.Length == 0;

    public static readonly HalfMatchResult Empty = default;

    public static bool operator >(HalfMatchResult left, HalfMatchResult right) => left.CommonMiddle.Length > right.CommonMiddle.Length;

    public static bool operator <(HalfMatchResult left, HalfMatchResult right) => left.CommonMiddle.Length < right.CommonMiddle.Length;
    public static HalfMatchResult operator -(HalfMatchResult item) => new(item.Prefix2, item.Suffix2, item.Prefix1, item.Suffix1, item.CommonMiddle);
    public override string ToString() => $"[{Prefix1}/{Prefix2}] - {CommonMiddle} - [{Suffix1}/{Suffix2}]";
}
