using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace DiffMatchPatch
{
    public static class TextUtil
    {
        /**
         * Determine the common prefix of two strings.
         * @param text1 First string.
         * @param text2 Second string.
         * @return The number of characters common to the start of each string.
         */
        internal static int CommonPrefix(string text1, string text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
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

        /**
         * Determine the common suffix of two strings.
         * @param text1 First string.
         * @param text2 Second string.
         * @return The number of characters common to the end of each string.
         */
        internal static int CommonSuffix(string text1, string text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            var text1Length = text1.Length;
            var text2Length = text2.Length;
            var n = Math.Min(text1.Length, text2.Length);
            for (var i = 1; i <= n; i++)
            {
                if (text1[text1Length - i] != text2[text2Length - i])
                {
                    return i - 1;
                }
            }
            return n;
        }

        /**
         * Determine if the suffix of one string is the prefix of another.
         * @param text1 First string.
         * @param text2 Second string.
         * @return The number of characters common to the end of the first
         *     string and the start of the second string.
         */
        internal static int CommonOverlap(string text1, string text2)
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
                text1 = text1.Substring(text1Length - text2Length);
            }
            else if (text1Length < text2Length)
            {
                text2 = text2.Substring(0, text1Length);
            }
            var textLength = Math.Min(text1Length, text2Length);
            // Quick check for the worst case.
            if (text1 == text2)
            {
                return textLength;
            }

            // Start by looking for a single character match
            // and increase length until no match is found.
            // Performance analysis: http://neil.fraser.name/news/2010/11/04/
            var best = 0;
            var length = 1;
            while (true)
            {
                var pattern = text1.Substring(textLength - length);
                var found = text2.IndexOf(pattern, StringComparison.Ordinal);
                if (found == -1)
                {
                    return best;
                }
                length += found;
                if (found == 0 || text1.Substring(textLength - length) ==
                    text2.Substring(0, length))
                {
                    best = length;
                    length++;
                }
            }

        }

        /**
         * Does a Substring of shorttext exist within longtext such that the
         * Substring is at least half the length of longtext?
         * @param longtext Longer string.
         * @param shorttext Shorter string.
         * @param i Start index of quarter length Substring within longtext.
         * @return half match result
         */

        private static HalfMatchResult HalfMatchI(string longtext, string shorttext, int i)
        {
            // Start with a 1/4 length Substring at position i as a seed.
            var seed = longtext.Substring(i, longtext.Length / 4);
            var j = -1;

            var bestCommon = string.Empty;
            string bestLongtextA = string.Empty, bestLongtextB = string.Empty;
            string bestShorttextA = string.Empty, bestShorttextB = string.Empty;
            
            while (j < shorttext.Length && (j = shorttext.IndexOf(seed, j + 1, StringComparison.Ordinal)) != -1)
            {
                var prefixLength = CommonPrefix(longtext.Substring(i), shorttext.Substring(j));
                var suffixLength = CommonSuffix(longtext.Substring(0, i), shorttext.Substring(0, j));
                if (bestCommon.Length < suffixLength + prefixLength)
                {
                    bestCommon = shorttext.Substring(j - suffixLength, suffixLength) + shorttext.Substring(j, prefixLength);
                    bestLongtextA = longtext.Substring(0, i - suffixLength);
                    bestLongtextB = longtext.Substring(i + prefixLength);
                    bestShorttextA = shorttext.Substring(0, j - suffixLength);
                    bestShorttextB = shorttext.Substring(j + prefixLength);
                }
            }
            return bestCommon.Length*2 >= longtext.Length
                ? new HalfMatchResult(bestLongtextA, bestLongtextB, bestShorttextA, bestShorttextB, bestCommon)
                : null;
        }

        /**
         * Do the two texts share a Substring which is at least half the length of
         * the longer text?
         * This speedup can produce non-minimal Diffs.
         * @param text1 First string.
         * @param text2 Second string.
         * @return Five element String array, containing the prefix of text1, the
         *     suffix of text1, the prefix of text2, the suffix of text2 and the
         *     common middle.  Or null if there was no match.
         */

        internal static HalfMatchResult HalfMatch(string text1, string text2)
        {
            var longtext = text1.Length > text2.Length ? text1 : text2;
            var shorttext = text1.Length > text2.Length ? text2 : text1;
            if (longtext.Length < 4 || shorttext.Length*2 < longtext.Length)
            {
                return null; // Pointless.
            }

            // First check if the second quarter is the seed for a half-match.
            var hm1 = HalfMatchI(longtext, shorttext, (longtext.Length + 3)/4);
            // Check again based on the third quarter.
            var hm2 = HalfMatchI(longtext, shorttext, (longtext.Length + 1)/2);
            
            HalfMatchResult hm;
            if (hm1 == null && hm2 == null)
            {
                return null;
            }
            
            if (hm2 == null)
            {
                hm = hm1;
            }
            else if (hm1 == null)
            {
                hm = hm2;
            }
            else
            {
                // Both matched.  Select the longest.
                hm = hm1.CommonMiddle.Length > hm2.CommonMiddle.Length ? hm1 : hm2;
            }

            // A half-match was found, sort out the return data.
            if (text1.Length > text2.Length)
            {
                return hm;
            }
            else
            {
                return new HalfMatchResult(hm.Prefix2, hm.Suffix2, hm.Prefix1, hm.Suffix1, hm.CommonMiddle);
            }
        }

        /**
         * Split two texts into a list of strings.  Reduce the texts to a string of
         * hashes where each Unicode character represents one line.
         * @param text1 First string.
         * @param text2 Second string.
         * @return Three element Object array, containing the encoded text1, the
         *     encoded text2 and the List of unique strings.  The zeroth element
         *     of the List of unique strings is intentionally blank.
         */
        internal static Tuple<string, string, List<string>> LinesToChars(string text1, string text2)
        {
            var lineArray = new List<string>();
            var lineHash = new Dictionary<string, int>();
            // e.g. linearray[4] == "Hello\n"
            // e.g. linehash.get("Hello\n") == 4

            // "\x00" is a valid character, but various debuggers don't like it.
            // So we'll insert a junk entry to avoid generating a null character.
            lineArray.Add(string.Empty);

            var chars1 = LinesToCharsMunge(text1, lineArray, lineHash);
            var chars2 = LinesToCharsMunge(text2, lineArray, lineHash);
            return Tuple.Create(chars1, chars2, lineArray);

        }

        /**
         * Split a text into a list of strings.  Reduce the texts to a string of
         * hashes where each Unicode character represents one line.
         * @param text String to encode.
         * @param lineArray List of unique strings.
         * @param lineHash Map of strings to indices.
         * @return Encoded string.
         */

        private static string LinesToCharsMunge(string text, List<string> lineArray, Dictionary<string, int> lineHash)
        {
            var lineStart = 0;
            var lineEnd = -1;
            string line;
            var chars = new StringBuilder();
            // Walk the text, pulling out a Substring for each line.
            // text.split('\n') would would temporarily double our memory footprint.
            // Modifying text would create many large strings to garbage collect.
            while (lineEnd < text.Length - 1)
            {
                lineEnd = text.IndexOf('\n', lineStart);
                if (lineEnd == -1)
                {
                    lineEnd = text.Length - 1;
                }
                line = text.JavaSubstring(lineStart, lineEnd + 1);
                lineStart = lineEnd + 1;

                if (lineHash.ContainsKey(line))
                {
                    chars.Append((char)lineHash[line]);
                }
                else
                {
                    lineArray.Add(line);
                    lineHash.Add(line, lineArray.Count - 1);
                    chars.Append((char)(lineArray.Count - 1));
                }
            }
            return chars.ToString();
        }

        /**
       * Unescape selected chars for compatability with JavaScript's encodeURI.
       * In speed critical applications this could be dropped since the
       * receiving application will certainly decode these fine.
       * Note that this function is case-sensitive.  Thus "%3F" would not be
       * unescaped.  But this is ok because it is only called with the output of
       * HttpUtility.UrlEncode which returns lowercase hex.
       *
       * Example: "%3f" -> "?", "%24" -> "$", etc.
       *
       * @param str The string to escape.
       * @return The escaped string.
       */
        internal static string UnescapeForEncodeUriCompatability(this string str)
        {
            return str.Replace("%21", "!").Replace("%7e", "~")
                .Replace("%27", "'").Replace("%28", "(").Replace("%29", ")")
                .Replace("%3b", ";").Replace("%2f", "/").Replace("%3f", "?")
                .Replace("%3a", ":").Replace("%40", "@").Replace("%26", "&")
                .Replace("%3d", "=").Replace("%2b", "+").Replace("%24", "$")
                .Replace("%2c", ",").Replace("%23", "#");
        }

        internal static string UrlEncoded(this string str)
        {
            return HttpUtility.UrlEncode(str, new UTF8Encoding());
        }

        //  MATCH FUNCTIONS


        /**
         * Locate the best instance of 'pattern' in 'text' near 'loc'.
         * Returns -1 if no match found.
         * @param text The text to search.
         * @param pattern The pattern to search for.
         * @param loc The location to search around.
         * @return Best match index or -1.
         */
        internal static int MatchPattern(this string text, string pattern, int loc)
        {
            return MatchPattern(text, pattern, loc, MatchSettings.Default);
        }
        internal static int MatchPattern(this string text, string pattern, int loc, MatchSettings settings)
        {
            // Check for null inputs not needed since null can't be passed in C#.

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
            return MatchBitap(text, pattern, loc, settings);
        }

        

        private static int MatchBitap(this string text, string pattern, int loc, MatchSettings settings)
        {
            var bitap = new BitapAlgorithm(settings);
            return bitap.Match(text, pattern, loc);

        }
    }
}