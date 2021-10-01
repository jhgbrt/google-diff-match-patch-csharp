namespace DiffMatchPatch;

/// <param name="PatchDeleteTreshold">
/// When deleting a large block of text (over ~64 characters), how close
/// do the contents have to be to match the expected contents. (0.0 =
/// perfection, 1.0 = very loose).  Note that Match_Threshold controls
/// how closely the end points of a delete need to match.
/// </param>
/// <param name="PatchMargin">
/// Chunk size for context length.
/// </param>
public readonly record struct PatchSettings(float PatchDeleteThreshold, short PatchMargin)
{
    public static PatchSettings Default { get; } = new PatchSettings(0.5f, 4);
}
