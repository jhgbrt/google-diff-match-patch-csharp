namespace DiffMatchPatch;

/// <summary>
/// 
/// </summary>
/// <param name="MatchTreshold">At what point is no match declared (0.0 = perfection, 1.0 = very loose).</param>
/// <param name="MatchDistance">
/// How far to search for a match (0 = exact location, 1000+ = broad match).
/// A match this many characters away from the expected location will add
/// 1.0 to the score (0.0 is a perfect match).
/// </param>
public readonly record struct MatchSettings(float MatchThreshold, int MatchDistance)
{
    public static MatchSettings Default { get; } = new MatchSettings(0.5f, 1000);
}
