namespace DiffMatchPatch;

internal static class TextUtil
{
    /// <summary>
    /// Determine the common prefix of two strings as the number of characters common to the start of each string.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <param name="i1">start index of substring in text1</param>
    /// <param name="i2">start index of substring in text2</param>
    /// <returns>The number of characters common to the start of each string.</returns>
    internal static int CommonPrefix(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2, int i1 = 0, int i2 = 0)
    {
        var l1 = text1.Length - i1;
        var l2 = text2.Length - i2;
        var n = Math.Min(l1, l2);
        for (var i = 0; i < n; i++)
        {
            if (text1[i + i1] != text2[i + i2])
            {
                return i;
            }
        }
        return n;
    }

    internal static int CommonPrefix(StringBuilder text1, StringBuilder text2)
    {
        var n = Math.Min(text1.Length, text2.Length);
        for (var i = 0; i < n; i++)
        {
            if (text1[i] != text2[i])
            {
                return i;
            }
        }
        return n;
    }
    /// <summary>
    /// Determine the common suffix of two strings as the number of characters common to the end of each string.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <param name="l1">maximum length to consider for text1</param>
    /// <param name="l2">maximum length to consider for text2</param>
    /// <returns>The number of characters common to the end of each string.</returns>
    internal static int CommonSuffix(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2, int? l1 = null, int? l2 = null)
    {
        var text1Length = l1 ?? text1.Length;
        var text2Length = l2 ?? text2.Length;
        var n = Math.Min(text1Length, text2Length);
        for (var i = 1; i <= n; i++)
        {
            if (text1[text1Length - i] != text2[text2Length - i])
            {
                return i - 1;
            }
        }
        return n;
    }
    internal static int CommonSuffix(StringBuilder text1, StringBuilder text2)
    {
        var text1Length = text1.Length;
        var text2Length = text2.Length;
        var n = Math.Min(text1Length, text2Length);
        for (var i = 1; i <= n; i++)
        {
            if (text1[text1Length - i] != text2[text2Length - i])
            {
                return i - 1;
            }
        }
        return n;
    }

    /// <summary>
    /// Determine if the suffix of one string is the prefix of another. Returns 
    /// the number of characters common to the end of the first
    /// string and the start of the second string.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <returns>The number of characters common to the end of the first
    ///  string and the start of the second string.</returns>
    internal static int CommonOverlap(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2)
    {
        // Cache the text lengths to prevent multiple calls.
        var text1Length = text1.Length;
        var text2Length = text2.Length;
        // Eliminate the null case.
        if (text1Length == 0 || text2Length == 0)
        {
            return 0;
        }
        // Truncate the longer string.
        if (text1Length > text2Length)
        {
            text1 = text1[(text1Length - text2Length)..];
        }
        else if (text1Length < text2Length)
        {
            text2 = text2.Slice(0, text1Length);
        }

        var textLength = Math.Min(text1Length, text2Length);

        // look for last character of text1 in text2, from the end to the beginning
        // where text1 ends with the pattern from beginning of text2 until that character
        var last = text1[^1];
        for (int length = text2.Length; length > 0; length--)
        {
            if (text2[length - 1] == last && text1.EndsWith(text2.Slice(0, length)))
                return length;
        }
        return 0;

    }

    /// <summary>
    /// Does a Substring of shorttext exist within longtext such that the
    /// Substring is at least half the length of longtext?
    /// </summary>
    /// <param name="longtext">Longer string.</param>
    /// <param name="shorttext">Shorter string.</param>
    /// <param name="i">Start index of quarter length Substring within longtext.</param>
    /// <returns></returns>
    private static HalfMatchResult HalfMatchI(ReadOnlySpan<char> longtext, ReadOnlySpan<char> shorttext, int i)
    {
        // Start with a 1/4 length Substring at position i as a seed.
        var seed = longtext.Slice(i, longtext.Length / 4);
        var j = -1;

        var bestCommon = string.Empty;
        string bestLongtextA = string.Empty, bestLongtextB = string.Empty;
        string bestShorttextA = string.Empty, bestShorttextB = string.Empty;

        int n = j;
        while (n < shorttext.Length && (j = shorttext[(j + 1)..].IndexOf(seed, StringComparison.Ordinal)) != -1)
        {
            j = n = j + n + 1;
            var prefixLength = CommonPrefix(longtext, shorttext, i, j);
            var suffixLength = CommonSuffix(longtext, shorttext, i, j);
            if (bestCommon.Length < suffixLength + prefixLength)
            {
                bestCommon = shorttext.Slice(j - suffixLength, suffixLength).ToString() + shorttext.Slice(j, prefixLength).ToString();
                bestLongtextA = longtext.Slice(0, i - suffixLength).ToString();
                bestLongtextB = longtext[(i + prefixLength)..].ToString();
                bestShorttextA = shorttext.Slice(0, j - suffixLength).ToString();
                bestShorttextB = shorttext[(j + prefixLength)..].ToString();
            }
        }
        return bestCommon.Length * 2 >= longtext.Length
            ? new(bestLongtextA, bestLongtextB, bestShorttextA, bestShorttextB, bestCommon)
            : HalfMatchResult.Empty;
    }


    /// <summary>
    /// Do the two texts share a Substring which is at least half the length of
    /// the longer text?
    /// This speedup can produce non-minimal Diffs.
    /// </summary>
    /// <param name="text1"></param>
    /// <param name="text2"></param>
    /// <returns>Data structure containing the prefix and suffix of string1, 
    /// the prefix and suffix of string 2, and the common middle. Null if there was no match.</returns>
    internal static HalfMatchResult HalfMatch(ReadOnlySpan<char> text1, ReadOnlySpan<char> text2)
    {
        var longtext = text1.Length > text2.Length ? text1 : text2;
        var shorttext = text1.Length > text2.Length ? text2 : text1;
        if (longtext.Length < 4 || shorttext.Length * 2 < longtext.Length)
        {
            return HalfMatchResult.Empty; // Pointless.
        }

        // First check if the second quarter is the seed for a half-match.
        var hm1 = HalfMatchI(longtext, shorttext, (longtext.Length + 3) / 4);
        // Check again based on the third quarter.
        var hm2 = HalfMatchI(longtext, shorttext, (longtext.Length + 1) / 2);

        if (hm1.IsEmpty && hm2.IsEmpty)
            return hm1;

        var hm = (hm1, hm2) switch
        {
            { hm2.IsEmpty: true } => hm1,
            { hm1.IsEmpty: true } => hm2,
            _ when hm1 > hm2 => hm1,
            _ => hm2
        };

        return text1.Length > text2.Length ? hm : hm.Reverse();
    }
    private static readonly Regex HEXCODE = new("%[0-9A-F][0-9A-F]");


    /// <summary>
    ///  Encodes a string with URI-style % escaping.
    /// Compatible with JavaScript's encodeURI function.
    /// </summary>
    internal static string UrlEncoded(this string str)
    {
        // TODO verify if this is the right way (probably should use HttpUtility here)

        int MAX_LENGTH = 0xFFEF;
        // C# throws a System.UriFormatException if string is too long.
        // Split the string into 64kb chunks.
        StringBuilder sb = new();
        int index = 0;
        while (index + MAX_LENGTH < str.Length)
        {
            sb.Append(Uri.EscapeDataString(str.Substring(index, MAX_LENGTH)));
            index += MAX_LENGTH;
        }
        sb.Append(Uri.EscapeDataString(str[index..]));
        // C# is overzealous in the replacements.  Walk back on a few.
        sb = sb.Replace('+', ' ').Replace("%20", " ").Replace("%21", "!")
            .Replace("%2A", "*").Replace("%27", "'").Replace("%28", "(")
            .Replace("%29", ")").Replace("%3B", ";").Replace("%2F", "/")
            .Replace("%3F", "?").Replace("%3A", ":").Replace("%40", "@")
            .Replace("%26", "&").Replace("%3D", "=").Replace("%2B", "+")
            .Replace("%24", "$").Replace("%2C", ",").Replace("%23", "#");
        // C# uses uppercase hex codes, JavaScript uses lowercase.

        return HEXCODE.Replace(sb.ToString(), s => s.Value.ToLower());
    }

    internal static string UrlDecoded(this string str) 
        => Uri.UnescapeDataString(str);

    //  MATCH FUNCTIONS

    /// <summary>
    /// Locate the best instance of 'pattern' in 'text' near 'loc'.
    /// Returns -1 if no match found.
    /// </summary>
    /// <param name="text">Text to search</param>
    /// <param name="pattern">pattern to search for</param>
    /// <param name="loc">location to search around</param>
    /// <returns>Best match index, -1 if not found</returns>
    internal static int FindBestMatchIndex(this string text, string pattern, int loc)
        => FindBestMatchIndex(text, pattern, loc, MatchSettings.Default);

    internal static int FindBestMatchIndex(this string text, string pattern, int loc, MatchSettings settings)
    {
        loc = Math.Max(0, Math.Min(loc, text.Length));
        if (text == pattern)
        {
            // Shortcut (potentially not guaranteed by the algorithm)
            return 0;
        }
        if (text.Length == 0)
        {
            // Nothing to match.
            return -1;
        }
        if (loc + pattern.Length <= text.Length
            && text.Substring(loc, pattern.Length) == pattern)
        {
            // Perfect match at the perfect spot!  (Includes case of null pattern)
            return loc;
        }

        // Do a fuzzy compare.
        var bitap = new BitapAlgorithm(settings);
        return bitap.Match(text, pattern, loc);
    }
}
