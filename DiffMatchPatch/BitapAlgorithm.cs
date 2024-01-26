namespace DiffMatchPatch;

/*
 * https://en.wikipedia.org/wiki/Bitap_algorithm
 */

/// <summary>
/// Implements the Bitap algorithm, a text search algorithm that allows for approximate string matching.
/// This class provides functionality to locate the best instance of a given pattern within a text string,
/// accounting for potential mismatches and errors. The algorithm is configured through MatchSettings which
/// includes match threshold and distance, determining the sensitivity and flexibility of the search.
/// </summary>
internal class BitapAlgorithm(MatchSettings settings)
{
    // Cost of an empty edit operation in terms of edit characters.
    // At what point is no match declared (0.0 = perfection, 1.0 = very loose).
    readonly float _matchThreshold = settings.MatchThreshold;
    // How far to search for a match (0 = exact location, 1000+ = broad match).
    // A match this many characters away from the expected location will add
    // 1.0 to the score (0.0 is a perfect match).
    readonly int _matchDistance = settings.MatchDistance;

    /// <summary>
    /// Locate the best instance of 'pattern' in 'text' near 'loc' using the
    /// Bitap algorithm.  Returns -1 if no match found.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <param name="pattern">The pattern to search for.</param>
    /// <param name="startIndex">The location to search around.</param>
    /// <returns>Best match index or -1.</returns>
    public int Match(string text, string pattern, int startIndex)
    {
        // Highest score beyond which we give up.
        double scoreThreshold = _matchThreshold;

        // Is there a nearby exact match? (speedup)
        var bestMatchIndex = text.IndexOf(pattern, startIndex, StringComparison.Ordinal);
        if (bestMatchIndex != -1)
        {
            scoreThreshold = Math.Min(MatchBitapScore(0, bestMatchIndex, startIndex, pattern), scoreThreshold);
            // What about in the other direction? (speedup)
            bestMatchIndex = text.LastIndexOf(pattern,
                Math.Min(startIndex + pattern.Length, text.Length),
                StringComparison.Ordinal);
            if (bestMatchIndex != -1)
            {
                scoreThreshold = Math.Min(MatchBitapScore(0, bestMatchIndex, startIndex, pattern), scoreThreshold);
            }
        }

        // Initialise the alphabet.
        var s = InitAlphabet(pattern);

        // Initialise the bit arrays.
        var matchmask = 1 << (pattern.Length - 1);
        bestMatchIndex = -1;

        int currentMinRange, currentMidpoint;
        var currentMaxRange = pattern.Length + text.Length;
        var lastComputedRow = Array.Empty<int>();
        for (var d = 0; d < pattern.Length; d++)
        {
            // Scan for the best match; each iteration allows for one more error.
            // Run a binary search to determine how far from 'loc' we can stray at
            // this error level.
            currentMinRange = 0;
            currentMidpoint = currentMaxRange;
            while (currentMinRange < currentMidpoint)
            {
                if (MatchBitapScore(d, startIndex + currentMidpoint, startIndex, pattern) <= scoreThreshold)
                    currentMinRange = currentMidpoint;
                else
                    currentMaxRange = currentMidpoint;
                currentMidpoint = (currentMaxRange - currentMinRange) / 2 + currentMinRange;
            }
            // Use the result from this iteration as the maximum for the next.
            currentMaxRange = currentMidpoint;
            var start = Math.Max(1, startIndex - currentMidpoint + 1);
            var finish = Math.Min(startIndex + currentMidpoint, text.Length) + pattern.Length;

            var rd = new int[finish + 2];
            rd[finish + 1] = (1 << d) - 1;
            for (var j = finish; j >= start; j--)
            {
                int charMatch;
                if (text.Length <= j - 1 || !s.ContainsKey(text[j - 1]))
                    // Out of range.
                    charMatch = 0;
                else
                    charMatch = s[text[j - 1]];

                if (d == 0)
                    // First pass: exact match.
                    rd[j] = ((rd[j + 1] << 1) | 1) & charMatch;
                else
                    // Subsequent passes: fuzzy match.
                    rd[j] = ((rd[j + 1] << 1) | 1) & charMatch | ((lastComputedRow[j + 1] | lastComputedRow[j]) << 1) | 1 | lastComputedRow[j + 1];

                if ((rd[j] & matchmask) != 0)
                {
                    var score = MatchBitapScore(d, j - 1, startIndex, pattern);
                    // This match will almost certainly be better than any existing
                    // match.  But check anyway.
                    if (score <= scoreThreshold)
                    {
                        // Told you so.
                        scoreThreshold = score;
                        bestMatchIndex = j - 1;
                        if (bestMatchIndex > startIndex)
                        {
                            // When passing loc, don't exceed our current distance from loc.
                            start = Math.Max(1, 2 * startIndex - bestMatchIndex);
                        }
                        else
                        {
                            // Already passed loc, downhill from here on in.
                            break;
                        }
                    }
                }
            }
            if (MatchBitapScore(d + 1, startIndex, startIndex, pattern) > scoreThreshold)
            {
                // No hope for a (better) match at greater error levels.
                break;
            }
            lastComputedRow = rd;
        }
        return bestMatchIndex;
    }

    /// <summary>
    /// Initialise the alphabet for the Bitap algorithm.
    /// </summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    internal static Dictionary<char, int> InitAlphabet(string pattern)
        => pattern
            .Select((c, i) => (c, i))
            .Aggregate(new Dictionary<char, int>(), (d, x) =>
            {
                d[x.c] = d.GetValueOrDefault(x.c) | (1 << (pattern.Length - x.i - 1)); 
                return d;
            });

    /// <summary>
    /// Compute and return the score for a match with e errors and x location.
    /// </summary>
    /// <param name="errors">Number of errors in match.</param>
    /// <param name="location">Location of match.</param>
    /// <param name="expectedLocation">Expected location of match.</param>
    /// <param name="pattern">Pattern being sought.</param>
    /// <returns>Overall score for match (0.0 = good, 1.0 = bad).</returns>
    private double MatchBitapScore(int errors, int location, int expectedLocation, string pattern)
    {
        var accuracy = (float)errors / pattern.Length;
        var proximity = Math.Abs(expectedLocation - location);

        return (_matchDistance, proximity) switch
        {
            (0, 0) => accuracy,
            (0, _) => 1.0,
            _ => accuracy + proximity / (float)_matchDistance
        };
    }
}
