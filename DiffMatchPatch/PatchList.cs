using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static DiffMatchPatch.Operation;

namespace DiffMatchPatch
{
    public static class PatchList
    {
        /// <summary>
        /// Given an array of patches, return another array that is identical.
        /// </summary>
        /// <param name="patches"></param>
        /// <returns></returns>
        private static List<Patch> DeepCopy(this IEnumerable<Patch> patches) => patches.Select(p => p.Copy()).ToList();

        /// <summary>
        /// Add some padding on text start and end so that edges can match something.
        /// Intended to be called only from within patch_apply.
        /// </summary>
        /// <param name="patches"></param>
        /// <param name="patchMargin"></param>
        /// <returns>The padding string added to each side.</returns>
        internal static string AddPadding(this List<Patch> patches, short patchMargin = 4)
        {
            var paddingLength = patchMargin;
            var nullPaddingSb = new StringBuilder();
            for (short x = 1; x <= paddingLength; x++)
            {
                nullPaddingSb.Append((char)x);
            }
            var nullPadding = nullPaddingSb.ToString();

            // Bump all the patches forward.
            foreach (var aPatch in patches)
            {
                aPatch.Start1 += paddingLength;
                aPatch.Start2 += paddingLength;
            }

            patches.First().AddPaddingBeforeFirstDiff(nullPadding);
            patches.Last().AddPaddingAfterLastDiff(nullPadding);

            return nullPadding;
        }

        /// <summary>
        /// Take a list of patches and return a textual representation.
        /// </summary>
        /// <param name="patches"></param>
        /// <returns></returns>
        public static string ToText(this List<Patch> patches) => patches.Aggregate(new StringBuilder(), (sb, patch) => sb.Append(patch)).ToString();

        static readonly Regex PatchHeader = new Regex("^@@ -(\\d+),?(\\d*) \\+(\\d+),?(\\d*) @@$");

        /// <summary>
        /// Parse a textual representation of patches and return a List of Patch
        /// objects.</summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static List<Patch> Parse(string text)
        {
            var patches = new List<Patch>();
            if (text.Length == 0)
            {
                return patches;
            }

            var lines = text.Split('\n');
            var index = 0;
            while (index < lines.Length)
            {
                var m = PatchHeader.Match(lines[index]);
                if (!m.Success)
                {
                    throw new ArgumentException("Invalid patch string: " + lines[index]);
                }

                (var start1, var length1) = m.GetStartLength(1, 2);
                (var start2, var length2) = m.GetStartLength(3, 4);

                index++;

                var diffs = new List<Diff>();
                while (index < lines.Length)
                {
                    if (!string.IsNullOrEmpty(lines[index]))
                    {
                        var sign = lines[index][0];
                        if (sign == '@')
                        {
                            // Start of next patch.
                            break;
                        }
                        var line = lines[index].Substring(1).Replace("+", "%2b").UrlDecoded();
                        diffs.Add(Diff.Create((Operation)sign, line));
                    }
                    index++;
                }

                var patch = new Patch
                (
                    start1,
                    length1,
                    start2,
                    length2,
                    diffs
                );
                patches.Add(patch);
            }
            return patches;
        }

        static (int start, int length) GetStartLength(this Match m, int startIndex, int lengthIndex)
        {
            var lengthStr = m.Groups[lengthIndex].Value;
            var value = Convert.ToInt32(m.Groups[startIndex].Value);
            switch (lengthStr)
            {
                case "0":
                    return (value, 0);
                case "":
                    return (value - 1, 1);
                default:
                    return (value - 1, Convert.ToInt32(lengthStr));
            }
        }
        
        /// <summary>
        /// Merge a set of patches onto the text.  Return a patched text, as well
        /// as an array of true/false values indicating which patches were applied.</summary>
        /// <param name="patches"></param>
        /// <param name="text">Old text</param>
        /// <returns>Two element Object array, containing the new text and an array of
        ///  bool values.</returns>

        public static (string newText, bool[] results) Apply(this List<Patch> patches, string text) 
            => Apply(patches, text, MatchSettings.Default, PatchSettings.Default);


        public static (string newText, bool[] results) Apply(this List<Patch> patches, string text, MatchSettings matchSettings) 
            => Apply(patches, text, matchSettings, PatchSettings.Default);

        /// <summary>
        /// Merge a set of patches onto the text.  Return a patched text, as well
        /// as an array of true/false values indicating which patches were applied.</summary>
        /// <param name="patches"></param>
        /// <param name="text">Old text</param>
        /// <param name="matchSettings"></param>
        /// <param name="settings"></param>
        /// <returns>Two element Object array, containing the new text and an array of
        ///  bool values.</returns>
        public static (string newText, bool[] results) Apply(this List<Patch> patches, string text,
            MatchSettings matchSettings, PatchSettings settings)
        {
            if (patches.Count == 0)
            {
                return (text, new bool[0]);
            }

            // Deep copy the patches so that no changes are made to originals.
            patches = patches.DeepCopy();

            var nullPadding = patches.AddPadding(settings.PatchMargin);
            text = nullPadding + text + nullPadding;
            patches.SplitMax();

            var x = 0;
            // delta keeps track of the offset between the expected and actual
            // location of the previous patch.  If there are patches expected at
            // positions 10 and 20, but the first patch was found at 12, delta is 2
            // and the second patch has an effective expected position of 22.
            var delta = 0;
            var results = new bool[patches.Count]; 
            foreach (var aPatch in patches)
            {
                var expectedLoc = aPatch.Start2 + delta;
                var text1 = aPatch.Diffs.Text1();
                int startLoc;
                var endLoc = -1;
                if (text1.Length > Constants.MatchMaxBits)
                {
                    // patch_splitMax will only provide an oversized pattern
                    // in the case of a monster delete.
                    startLoc = text.FindBestMatchIndex(text1.Substring(0, Constants.MatchMaxBits), expectedLoc, matchSettings);
                    // Check for null inputs not needed since null can't be passed in C#.
                    if (startLoc != -1)
                    {
                        endLoc = text.FindBestMatchIndex(
                            text1.Substring(text1.Length - Constants.MatchMaxBits), expectedLoc + text1.Length - Constants.MatchMaxBits, matchSettings
                            );
                        // Check for null inputs not needed since null can't be passed in C#.
                        if (endLoc == -1 || startLoc >= endLoc)
                        {
                            // Can't find valid trailing context.  Drop this patch.
                            startLoc = -1;
                        }
                    }
                }
                else
                {
                    startLoc = text.FindBestMatchIndex(text1, expectedLoc, matchSettings);
                    // Check for null inputs not needed since null can't be passed in C#.
                }
                if (startLoc == -1)
                {
                    // No match found.  :(
                    results[x] = false;
                    // Subtract the delta for this failed patch from subsequent patches.
                    delta -= aPatch.Length2 - aPatch.Length1;
                }
                else
                {
                    // Found a match.  :)
                    results[x] = true;
                    delta = startLoc - expectedLoc;
                    int actualEndLoc;
                    if (endLoc == -1)
                    {
                        actualEndLoc = Math.Min(startLoc + text1.Length, text.Length);
                    }
                    else
                    {
                        actualEndLoc = Math.Min(endLoc + Constants.MatchMaxBits, text.Length);
                    }
                    var text2 = text.Substring(startLoc, actualEndLoc - startLoc);
                    if (text1 == text2)
                    {
                        // Perfect match, just shove the Replacement text in.
                        text = text.Substring(0, startLoc) + aPatch.Diffs.Text2()
                               + text.Substring(startLoc + text1.Length);
                    }
                    else
                    {
                        // Imperfect match.  Run a diff to get a framework of equivalent
                        // indices.
                        var diffs = Diff.Compute(text1, text2, 0f, false);
                        if (text1.Length > Constants.MatchMaxBits
                            && diffs.Levenshtein() / (float)text1.Length
                            > settings.PatchDeleteThreshold)
                        {
                            // The end points match, but the content is unacceptably bad.
                            results[x] = false;
                        }
                        else
                        {
                            diffs.CleanupSemanticLossless();
                            var index1 = 0;
                            foreach (var aDiff in aPatch.Diffs)
                            {
                                if (aDiff.Operation != Equal)
                                {
                                    var index2 = diffs.FindEquivalentLocation2(index1);
                                    if (aDiff.Operation == Insert)
                                    {
                                        // Insertion
                                        text = text.Insert(startLoc + index2, aDiff.Text);
                                    }
                                    else if (aDiff.Operation == Delete)
                                    {
                                        // Deletion
                                        text = text.Remove(startLoc + index2, diffs.FindEquivalentLocation2(index1 + aDiff.Text.Length) - index2);
                                    }
                                }
                                if (aDiff.Operation != Delete)
                                {
                                    index1 += aDiff.Text.Length;
                                }
                            }
                        }
                    }
                }
                x++;
            }
            // Strip the padding off.
            text = text.Substring(nullPadding.Length, text.Length
                                                      - 2 * nullPadding.Length);
            return (text, results);
        }

        /// <summary>
        /// Look through the patches and break up any which are longer than the
        /// maximum limit of the match algorithm.
        /// Intended to be called only from within patch_apply.
        ///  </summary>
        /// <param name="patches"></param>
        /// <param name="patchMargin"></param>
        internal static void SplitMax(this List<Patch> patches, short patchMargin = 4)
        {
            var patchSize = Constants.MatchMaxBits;
            for (var x = 0; x < patches.Count; x++)
            {
                if (patches[x].Length1 <= patchSize)
                {
                    continue;
                }
                var bigpatch = patches[x];
                // Remove the big old patch.
                patches.Splice(x--, 1);
                var start1 = bigpatch.Start1;
                var start2 = bigpatch.Start2;
                var precontext = string.Empty;
                var diffs = bigpatch.Diffs;
                while (diffs.Count != 0)
                {
                    // Create one of several smaller patches.
                    var patch = new Patch();
                    var empty = true;
                    patch.Start1 = start1 - precontext.Length;
                    patch.Start2 = start2 - precontext.Length;
                    if (precontext.Length != 0)
                    {
                        patch.Length1 = patch.Length2 = precontext.Length;
                        patch.AddDiff(Diff.Equal(precontext));
                    }
                    while (diffs.Any() && patch.Length1 < patchSize - patchMargin)
                    {
                        var diffType = diffs[0].Operation;
                        var diffText = diffs[0].Text;
                        if (diffType == Insert)
                        {
                            // Insertions are harmless.
                            patch.Length2 += diffText.Length;
                            start2 += diffText.Length;
                            patch.AddDiff(diffs.First());
                            diffs.RemoveAt(0);
                            empty = false;
                        }
                        else if (diffType == Delete && patch.Diffs.Count == 1
                                 && patch.Diffs.First().Operation == Equal
                                 && diffText.Length > 2 * patchSize)
                        {
                            // This is a large deletion.  Let it pass in one chunk.
                            patch.Length1 += diffText.Length;
                            start1 += diffText.Length;
                            empty = false;
                            patch.AddDiff(Diff.Create(diffType, diffText));
                            diffs.RemoveAt(0);
                        }
                        else
                        {
                            // Deletion or equality.  Only take as much as we can stomach.
                            diffText = diffText.Substring(0, Math.Min(diffText.Length,
                                patchSize - patch.Length1 - patchMargin));
                            patch.Length1 += diffText.Length;
                            start1 += diffText.Length;
                            if (diffType == Equal)
                            {
                                patch.Length2 += diffText.Length;
                                start2 += diffText.Length;
                            }
                            else
                            {
                                empty = false;
                            }
                            patch.AddDiff(Diff.Create(diffType, diffText));
                            if (diffText == diffs[0].Text)
                            {
                                diffs.RemoveAt(0);
                            }
                            else
                            {
                                diffs[0] = diffs[0].Replace(diffs[0].Text.Substring(diffText.Length));
                            }
                        }
                    }
                    // Compute the head context for the next patch.
                    precontext = patch.Diffs.Text2();
                    precontext = precontext.Substring(Math.Max(0,
                        precontext.Length - patchMargin));

                    // Append the end context for this patch.
                    var text1 = diffs.Text1();
                    var postcontext = text1.Length > patchMargin ? text1.Substring(0, patchMargin) : text1;

                    if (postcontext.Length != 0)
                    {
                        patch.Length1 += postcontext.Length;
                        patch.Length2 += postcontext.Length;
                        var lastDiff = patch.Diffs.Last();
                        if (patch.Diffs.Any() && lastDiff.Operation == Equal)
                        {
                            patch.Diffs[patch.Diffs.Count - 1] = lastDiff.Replace(lastDiff.Text + postcontext);
                        }
                        else
                        {
                            patch.AddDiff(Diff.Equal(postcontext));
                        }
                    }
                    if (!empty)
                    {
                        patches.Splice(++x, 0, patch);
                    }
                }
            }
        }
    }
}