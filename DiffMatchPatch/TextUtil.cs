namespace DiffMatchPatch;

internal static class TextUtil
{
    /// <summary>
    /// Ordinal (content-based) hash of a span of characters (FNV-1a).
    /// </summary>
    internal static int OrdinalHash(ReadOnlySpan<char> span)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in span)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }

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
    private static HalfMatchResult HalfMatchI(StringSlice longtext, StringSlice shorttext, int i)
    {
        var longtextSpan = longtext.Span;
        var shorttextSpan = shorttext.Span;
        var seed = longtextSpan.Slice(i, longtextSpan.Length / 4);
        var j = -1;

        var bestCommonLength = 0;
        int bestLongtextAStart = 0, bestLongtextALength = 0;
        int bestLongtextBStart = 0, bestLongtextBLength = 0;
        int bestShorttextAStart = 0, bestShorttextALength = 0;
        int bestShorttextBStart = 0, bestShorttextBLength = 0;
        int bestShorttextCommonStart = 0, bestShorttextCommonLength = 0;

        int n = j;
        while (n < shorttextSpan.Length && (j = shorttextSpan[(j + 1)..].IndexOf(seed, StringComparison.Ordinal)) != -1)
        {
            j = n = j + n + 1;
            var prefixLength = CommonPrefix(longtextSpan, shorttextSpan, i, j);
            var suffixLength = CommonSuffix(longtextSpan, shorttextSpan, i, j);
            var totalCommon = suffixLength + prefixLength;
            if (bestCommonLength < totalCommon)
            {
                bestCommonLength = totalCommon;
                bestLongtextAStart = 0;
                bestLongtextALength = i - suffixLength;
                bestLongtextBStart = i + prefixLength;
                bestLongtextBLength = longtextSpan.Length - bestLongtextBStart;
                bestShorttextAStart = 0;
                bestShorttextALength = j - suffixLength;
                bestShorttextBStart = j + prefixLength;
                bestShorttextBLength = shorttextSpan.Length - bestShorttextBStart;
                bestShorttextCommonStart = j - suffixLength;
                bestShorttextCommonLength = suffixLength + prefixLength;
            }
        }

        if (bestCommonLength * 2 >= longtextSpan.Length)
        {
            return new HalfMatchResult(
                longtext.Slice(bestLongtextAStart, bestLongtextALength),
                longtext.Slice(bestLongtextBStart, bestLongtextBLength),
                shorttext.Slice(bestShorttextAStart, bestShorttextALength),
                shorttext.Slice(bestShorttextBStart, bestShorttextBLength),
                shorttext.Slice(bestShorttextCommonStart, bestShorttextCommonLength)
            );
        }
        return HalfMatchResult.Empty;
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
    internal static HalfMatchResult HalfMatch(StringSlice text1, StringSlice text2)
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

        var hm = (hm1, hm2) switch
        {
            { hm1.IsEmpty: true } and { hm2.IsEmpty: true } => hm1,
            { hm2.IsEmpty: true } => hm1,
            { hm1.IsEmpty: true } => hm2,
            _ when hm1 > hm2 => hm1,
            _ => hm2
        };

        return text1.Length > text2.Length ? hm : -hm;
    }


    /// <summary>
    ///  Encodes a string with URI-style % escaping.
    /// Compatible with JavaScript's encodeURI function.
    /// </summary>
    internal static string UrlEncoded(this string str) => new StringBuilder(str.Length).AppendUrlEncoded(str.AsSpan()).ToString();

    private const string HexDigits = "0123456789abcdef";

    /// <summary>
    /// Append text with %xx (UTF-8, lowercase hex) escaping, compatible with JavaScript's encodeURI:
    /// letters, digits and <c>-_.~ !*'();/?:@&amp;=+$,#</c> are not escaped.
    /// Unpaired surrogates are encoded as U+FFFD.
    /// </summary>
    internal static StringBuilder AppendUrlEncoded(this StringBuilder sb, ReadOnlySpan<char> text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsUnescaped(c))
            {
                sb.Append(c);
                continue;
            }

            int scalar;
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                scalar = char.ConvertToUtf32(c, text[i + 1]);
                i++;
            }
            else
            {
                scalar = char.IsSurrogate(c) ? 0xFFFD : c;
            }

            if (scalar < 0x80)
            {
                AppendEscaped(sb, scalar);
            }
            else if (scalar < 0x800)
            {
                AppendEscaped(sb, 0xC0 | (scalar >> 6));
                AppendEscaped(sb, 0x80 | (scalar & 0x3F));
            }
            else if (scalar < 0x10000)
            {
                AppendEscaped(sb, 0xE0 | (scalar >> 12));
                AppendEscaped(sb, 0x80 | ((scalar >> 6) & 0x3F));
                AppendEscaped(sb, 0x80 | (scalar & 0x3F));
            }
            else
            {
                AppendEscaped(sb, 0xF0 | (scalar >> 18));
                AppendEscaped(sb, 0x80 | ((scalar >> 12) & 0x3F));
                AppendEscaped(sb, 0x80 | ((scalar >> 6) & 0x3F));
                AppendEscaped(sb, 0x80 | (scalar & 0x3F));
            }
        }
        return sb;

        static void AppendEscaped(StringBuilder sb, int b)
            => sb.Append('%').Append(HexDigits[b >> 4]).Append(HexDigits[b & 0xF]);
    }

    private static bool IsUnescaped(char c) => c switch
    {
        >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' => true,
        '-' or '_' or '.' or '~' or ' ' or '!' or '*' or '\'' or '(' or ')' or ';' or '/' or '?'
            or ':' or '@' or '&' or '=' or '+' or '$' or ',' or '#' => true,
        _ => false
    };

    internal static string UrlDecoded(this string str) => Uri.UnescapeDataString(str);

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
            && text.AsSpan(loc, pattern.Length).SequenceEqual(pattern))
        {
            // Perfect match at the perfect spot!  (Includes case of null pattern)
            return loc;
        }

        // Do a fuzzy compare.
        var bitap = new BitapAlgorithm(settings);
        return bitap.Match(text, pattern, loc);
    }
}
