using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiffMatchPatch
{
    public class Patch
    {
        private List<Diff> _diffs = new List<Diff>();
        public List<Diff> Diffs {get { return _diffs; }}
        public int Start1 { get; internal set; }
        public int Start2 { get; internal set; }
        public int Length1 { get; internal set; }
        public int Length2 { get; internal set; }

        /// <summary>
        /// Generate GNU diff's format.
        /// Header: @@ -382,8 +481,9 @@
        /// Indicies are printed as 1-based, not 0-based.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string coords1, coords2;
            if (Length1 == 0)
            {
                coords1 = Start1 + ",0";
            }
            else if (Length1 == 1)
            {
                coords1 = Convert.ToString(Start1 + 1);
            }
            else
            {
                coords1 = Start1 + 1 + "," + Length1;
            }
            if (Length2 == 0)
            {
                coords2 = Start2 + ",0";
            }
            else if (Length2 == 1)
            {
                coords2 = Convert.ToString(Start2 + 1);
            }
            else
            {
                coords2 = Start2 + 1 + "," + Length2;
            }
            var text = new StringBuilder();
            text.Append("@@ -")
                .Append(coords1)
                .Append(" +")
                .Append(coords2)
                .Append(" @@\n");
            // Escape the body of the patch with %xx notation.
            foreach (var aDiff in Diffs)
            {
                switch (aDiff.Operation)
                {
                    case Operation.Insert:
                        text.Append('+');
                        break;
                    case Operation.Delete:
                        text.Append('-');
                        break;
                    case Operation.Equal:
                        text.Append(' ');
                        break;
                }

                text.Append(aDiff.Text.UrlEncoded().Replace('+', ' ')).Append("\n");
            }

            return text.ToString().UnescapeForEncodeUriCompatability();
        }

        internal Patch Copy()
        {
            var patchCopy = new Patch
            {
                Start1 = Start1,
                Start2 = Start2,
                Length1 = Length1,
                Length2 = Length2,
                _diffs = (from d in Diffs select d.Copy()).ToList()
            };
            return patchCopy;
        }

        /// <summary>
        /// Increase the context until it is unique,
        /// but don't let the pattern expand beyond Match_MaxBits.</summary>
        /// <param name="text">Source text</param>
        /// <param name="patchMargin"></param>
        internal void AddContext(string text, short patchMargin = 4)
        {
            if (text.Length == 0)
            {
                return;
            }
            var pattern = text.Substring(Start2, Length1);
            var padding = 0;

            // Look for the first and last matches of pattern in text.  If two
            // different matches are found, increase the pattern length.
            while (text.IndexOf(pattern, StringComparison.Ordinal)
                   != text.LastIndexOf(pattern, StringComparison.Ordinal)
                   && pattern.Length < Constants.MatchMaxBits - patchMargin - patchMargin)
            {
                padding += patchMargin;
                int begin = Math.Max(0, Start2 - padding);
                pattern = text.Substring(begin, Math.Min(text.Length, Start2 + Length1 + padding) - begin);
            }
            // Add one chunk for good luck.
            padding += patchMargin;

            // Add the prefix.
            int begin1 = Math.Max(0, Start2 - padding);
            var prefix = text.Substring(begin1, Start2 - begin1);
            if (prefix.Length != 0)
            {
                Diffs.Insert(0, Diff.Equal(prefix));
            }
            // Add the suffix.
            int begin2 = Start2 + Length1;
            var length = Math.Min(text.Length, Start2 + Length1 + padding) - begin2;
            var suffix = text.Substring(begin2, length);
            if (suffix.Length != 0)
            {
                Diffs.Add(Diff.Equal(suffix));
            }

            // Roll back the start points.
            Start1 -= prefix.Length;
            Start2 -= prefix.Length;
            // Extend the lengths.
            Length1 += prefix.Length + suffix.Length;
            Length2 += prefix.Length + suffix.Length;
        }

        /// <summary>
        /// Compute a list of patches to turn text1 into text2.
        /// A set of Diffs will be computed.
        /// </summary>
        /// <param name="text1">old text</param>
        /// <param name="text2">new text</param>
        /// <param name="diffTimeout">timeout in seconds</param>
        /// <param name="diffEditCost">Cost of an empty edit operation in terms of edit characters.</param>
        /// <returns>List of Patch objects</returns>
        public static List<Patch> Compute(string text1, string text2, float diffTimeout = 0, short diffEditCost = 4)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            // No Diffs provided, compute our own.
            var diffs = Diff.Compute(text1, text2, diffTimeout);
            diffs.CleanupSemantic();
            diffs.CleanupEfficiency(diffEditCost);
            return Compute(text1, diffs);
        }

        /// <summary>
        /// Compute a list of patches to turn text1 into text2.
        /// text1 will be derived from the provided Diffs.
        /// </summary>
        /// <param name="diffs">array of diff objects for text1 to text2</param>
        /// <returns>List of Patch objects</returns>
        public static List<Patch> FromDiffs(List<Diff> diffs)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            // No origin string provided, compute our own.
            var text1 = diffs.Text1();
            return Compute(text1, diffs);
        }

        /// <summary>
        /// Compute a list of patches to turn text1 into text2.
        /// text2 is not provided, Diffs are the delta between text1 and text2.
        /// </summary>
        /// <param name="text1"></param>
        /// <param name="diffs"></param>
        /// <param name="patchMargin"></param>
        /// <returns></returns>
        public static List<Patch> Compute(string text1, List<Diff> diffs, short patchMargin = 4)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            var patches = new List<Patch>();
            if (diffs.Count == 0)
            {
                return patches;  // Get rid of the null case.
            }
            var patch = new Patch();
            var charCount1 = 0;  // Number of characters into the text1 string.
            var charCount2 = 0;  // Number of characters into the text2 string.
            // Start with text1 (prepatch_text) and apply the Diffs until we arrive at
            // text2 (postpatch_text). We recreate the patches one by one to determine
            // context info.
            var prepatchText = text1;
            var postpatchText = text1;
            foreach (var aDiff in diffs)
            {
                if (patch.Diffs.Count == 0 && aDiff.Operation != Operation.Equal)
                {
                    // A new patch starts here.
                    patch.Start1 = charCount1;
                    patch.Start2 = charCount2;
                }

                switch (aDiff.Operation)
                {
                    case Operation.Insert:
                        patch.Diffs.Add(aDiff);
                        patch.Length2 += aDiff.Text.Length;
                        postpatchText = postpatchText.Insert(charCount2, aDiff.Text);
                        break;
                    case Operation.Delete:
                        patch.Length1 += aDiff.Text.Length;
                        patch.Diffs.Add(aDiff);
                        postpatchText = postpatchText.Remove(charCount2,
                            aDiff.Text.Length);
                        break;
                    case Operation.Equal:
                        if (aDiff.Text.Length <= 2 * patchMargin
                            && patch.Diffs.Count() != 0 && aDiff != diffs.Last())
                        {
                            // Small equality inside a patch.
                            patch.Diffs.Add(aDiff);
                            patch.Length1 += aDiff.Text.Length;
                            patch.Length2 += aDiff.Text.Length;
                        }

                        if (aDiff.Text.Length >= 2 * patchMargin)
                        {
                            // Time for a new patch.
                            if (patch.Diffs.Count != 0)
                            {
                                patch.AddContext(prepatchText);
                                patches.Add(patch);
                                patch = new Patch();
                                // Unlike Unidiff, our patch lists have a rolling context.
                                // http://code.google.com/p/google-diff-match-patch/wiki/Unidiff
                                // Update prepatch text & pos to reflect the application of the
                                // just completed patch.
                                prepatchText = postpatchText;
                                charCount1 = charCount2;
                            }
                        }
                        break;
                }

                // Update the current character count.
                if (aDiff.Operation != Operation.Insert)
                {
                    charCount1 += aDiff.Text.Length;
                }
                if (aDiff.Operation != Operation.Delete)
                {
                    charCount2 += aDiff.Text.Length;
                }
            }
            // Pick up the leftover patch if not empty.
            if (patch.Diffs.Count != 0)
            {
                patch.AddContext(prepatchText);
                patches.Add(patch);
            }

            return patches;
        }
    }
}