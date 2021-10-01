namespace DiffMatchPatch;

internal readonly record struct HalfMatchResult(string Prefix1, string Suffix1, string Prefix2, string Suffix2, string CommonMiddle)
{
    public HalfMatchResult Reverse() => new(Prefix2, Suffix2, Prefix1, Suffix1, CommonMiddle);

    public bool IsEmpty => string.IsNullOrEmpty(CommonMiddle);

    public static readonly HalfMatchResult Empty = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    public static bool operator >(HalfMatchResult left, HalfMatchResult right) => left.CommonMiddle.Length > right.CommonMiddle.Length;

    public static bool operator <(HalfMatchResult left, HalfMatchResult right) => left.CommonMiddle.Length < right.CommonMiddle.Length;

    public override string ToString() => $"[{Prefix1}/{Prefix2}] - {CommonMiddle} - [{Suffix1}/{Suffix2}]";
}
