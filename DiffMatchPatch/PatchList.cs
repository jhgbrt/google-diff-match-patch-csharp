using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace DiffMatchPatch
{
    public static class PatchList
    {
        /**
         * Given an array of patches, return another array that is identical.
         * @param patches Array of Patch objects.
         * @return Array of Patch objects.
         */
        internal static List<Patch> DeepCopy(this List<Patch> patches)
        {
            return (from p in patches select p.Copy()).ToList();
        }

        /**
         * Add some padding on text start and end so that edges can match something.
         * Intended to be called only from within patch_apply.
         * @param patches Array of Patch objects.
         * @return The padding string added to each side.
         */
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

            // Add some padding on start of first diff.
            var patch = patches.First();
            var diffs = patch.Diffs;
            if (diffs.Count == 0 || diffs[0].Operation != Operation.Equal)
            {
                // Add nullPadding equality.
                diffs.Insert(0, Diff.Equal(nullPadding));
                patch.Start1 -= paddingLength;  // Should be 0.
                patch.Start2 -= paddingLength;  // Should be 0.
                patch.Length1 += paddingLength;
                patch.Length2 += paddingLength;
            }
            else if (paddingLength > diffs[0].Text.Length)
            {
                // Grow first equality.
                var firstDiff = diffs[0];
                var extraLength = nullPadding.Length - firstDiff.Text.Length;
                diffs[0] = firstDiff.Replace(nullPadding.Substring(firstDiff.Text.Length) + firstDiff.Text);
                patch.Start1 -= extraLength;
                patch.Start2 -= extraLength;
                patch.Length1 += extraLength;
                patch.Length2 += extraLength;
            }

            // Add some padding on end of last diff.
            patch = patches.Last();
            diffs = patch.Diffs;
            if (diffs.Count == 0 || diffs.Last().Operation != Operation.Equal)
            {
                // Add nullPadding equality.
                diffs.Add(Diff.Equal(nullPadding));
                patch.Length1 += paddingLength;
                patch.Length2 += paddingLength;
            }
            else if (paddingLength > diffs[diffs.Count - 1].Text.Length)
            {
                // Grow last equality.
                var lastDiff = diffs[diffs.Count - 1];
                var extraLength = nullPadding.Length - lastDiff.Text.Length;
                var text = lastDiff.Text + nullPadding.Substring(0, extraLength);
                diffs[diffs.Count - 1] = lastDiff.Replace(text);
                patch.Length1 += extraLength;
                patch.Length2 += extraLength;
            }

            return nullPadding;
        }

        /**
         * Take a list of patches and return a textual representation.
         * @param patches List of Patch objects.
         * @return Text representation of patches.
         */
        public static string ToText(this List<Patch> patches)
        {
            var text = new StringBuilder();
            foreach (var aPatch in patches)
            {
                text.Append(aPatch);
            }
            return text.ToString();
        }

        /**
              * Parse a textual representation of patches and return a List of Patch
              * objects.
              * @param textline Text representation of patches.
              * @return List of Patch objects.
              * @throws ArgumentException If invalid input.
              */
        public static List<Patch> Parse(string textline)
        {
            var patches = new List<Patch>();
            if (textline.Length == 0)
            {
                return patches;
            }
            var patchHeader = new Regex("^@@ -(\\d+),?(\\d*) \\+(\\d+),?(\\d*) @@$");
            var text = textline.Split('\n');
            var textPointer = 0;
            while (textPointer < text.Length)
            {
                var m = patchHeader.Match(text[textPointer]);
                if (!m.Success)
                {
                    throw new ArgumentException("Invalid patch string: " + text[textPointer]);
                }
                var patch = new Patch();
                patches.Add(patch);
                patch.Start1 = Convert.ToInt32(m.Groups[1].Value);
                if (m.Groups[2].Length == 0)
                {
                    patch.Start1--;
                    patch.Length1 = 1;
                }
                else if (m.Groups[2].Value == "0")
                {
                    patch.Length1 = 0;
                }
                else
                {
                    patch.Start1--;
                    patch.Length1 = Convert.ToInt32(m.Groups[2].Value);
                }

                patch.Start2 = Convert.ToInt32(m.Groups[3].Value);
                if (m.Groups[4].Length == 0)
                {
                    patch.Start2--;
                    patch.Length2 = 1;
                }
                else if (m.Groups[4].Value == "0")
                {
                    patch.Length2 = 0;
                }
                else
                {
                    patch.Start2--;
                    patch.Length2 = Convert.ToInt32(m.Groups[4].Value);
                }
                textPointer++;

                while (textPointer < text.Length)
                {
                    if (string.IsNullOrEmpty(text[textPointer]))
                    {
                        textPointer++;
                        continue;
                    }

                    var sign = text[textPointer][0];
                    var line = text[textPointer].Substring(1);
                    line = line.Replace("+", "%2b");
                    line = HttpUtility.UrlDecode(line, new UTF8Encoding(false, true));
                    if (sign == '-')
                    {
                        // Deletion.
                        patch.Diffs.Add(Diff.Delete(line));
                    }
                    else if (sign == '+')
                    {
                        // Insertion.
                        patch.Diffs.Add(Diff.Insert(line));
                    }
                    else if (sign == ' ')
                    {
                        // Minor equality.
                        patch.Diffs.Add(Diff.Equal(line));
                    }
                    else if (sign == '@')
                    {
                        // Start of next patch.
                        break;
                    }
                    else
                    {
                        // WTF?
                        throw new ArgumentException(
                            "Invalid patch mode '" + sign + "' in: " + line);
                    }
                    textPointer++;
                }
            }
            return patches;
        }

        /**
         * Merge a set of patches onto the text.  Return a patched text, as well
         * as an array of true/false values indicating which patches were applied.
         * @param patches Array of Patch objects
         * @param text Old text.
         * @return Two element Object array, containing the new text and an array of
         *      bool values.
         */

        public static Tuple<string, bool[]> Apply(this List<Patch> patches, string text)
        {
            return Apply(patches, text, MatchSettings.Default);
        }

        /**
         * Merge a set of patches onto the text.  Return a patched text, as well
         * as an array of true/false values indicating which patches were applied.
         * @param patches Array of Patch objects
         * @param text Old text.
         * @return Two element Object array, containing the new text and an array of
         *      bool values.
         */
        public static Tuple<string, bool[]> Apply(this List<Patch> patches, string text, 
            MatchSettings matchSettings, PatchSettings settings = null
            )
        {
            settings = settings ?? PatchSettings.Default;
            if (patches.Count == 0)
            {
                return Tuple.Create(text, new bool[0]);
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
                    startLoc = text.MatchPattern(text1.Substring(0, Constants.MatchMaxBits), expectedLoc, matchSettings);
                    // Check for null inputs not needed since null can't be passed in C#.
                    if (startLoc != -1)
                    {
                        endLoc = text.MatchPattern(
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
                    startLoc = text.MatchPattern(text1, expectedLoc, matchSettings);
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
                    string text2;
                    if (endLoc == -1)
                    {
                        text2 = text.JavaSubstring(startLoc,
                            Math.Min(startLoc + text1.Length, text.Length));
                    }
                    else
                    {
                        text2 = text.JavaSubstring(startLoc,
                            Math.Min(endLoc + Constants.MatchMaxBits, text.Length));
                    }
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
                                if (aDiff.Operation != Operation.Equal)
                                {
                                    var index2 = diffs.FindEquivalentLocation2(index1);
                                    if (aDiff.Operation == Operation.Insert)
                                    {
                                        // Insertion
                                        text = text.Insert(startLoc + index2, aDiff.Text);
                                    }
                                    else if (aDiff.Operation == Operation.Delete)
                                    {
                                        // Deletion
                                        text = text.Remove(startLoc + index2, diffs.FindEquivalentLocation2(index1 + aDiff.Text.Length) - index2);
                                    }
                                }
                                if (aDiff.Operation != Operation.Delete)
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
            return Tuple.Create(text, results);
        }


        /**
         * Look through the patches and break up any which are longer than the
         * maximum limit of the match algorithm.
         * Intended to be called only from within patch_apply.
         * @param patches List of Patch objects.
         */
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
                        patch.Diffs.Add(Diff.Equal(precontext));
                    }
                    while (diffs.Count != 0
                           && patch.Length1 < patchSize - patchMargin)
                    {
                        var diffType = diffs[0].Operation;
                        var diffText = diffs[0].Text;
                        if (diffType == Operation.Insert)
                        {
                            // Insertions are harmless.
                            patch.Length2 += diffText.Length;
                            start2 += diffText.Length;
                            patch.Diffs.Add(diffs.First());
                            diffs.RemoveAt(0);
                            empty = false;
                        }
                        else if (diffType == Operation.Delete && patch.Diffs.Count == 1
                                 && patch.Diffs.First().Operation == Operation.Equal
                                 && diffText.Length > 2 * patchSize)
                        {
                            // This is a large deletion.  Let it pass in one chunk.
                            patch.Length1 += diffText.Length;
                            start1 += diffText.Length;
                            empty = false;
                            patch.Diffs.Add(Diff.Create(diffType, diffText));
                            diffs.RemoveAt(0);
                        }
                        else
                        {
                            // Deletion or equality.  Only take as much as we can stomach.
                            diffText = diffText.Substring(0, Math.Min(diffText.Length,
                                patchSize - patch.Length1 - patchMargin));
                            patch.Length1 += diffText.Length;
                            start1 += diffText.Length;
                            if (diffType == Operation.Equal)
                            {
                                patch.Length2 += diffText.Length;
                                start2 += diffText.Length;
                            }
                            else
                            {
                                empty = false;
                            }
                            patch.Diffs.Add(Diff.Create(diffType, diffText));
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

                    string postcontext = null;
                    // Append the end context for this patch.
                    if (diffs.Text1().Length > patchMargin)
                    {
                        postcontext = diffs.Text1()
                            .Substring(0, patchMargin);
                    }
                    else
                    {
                        postcontext = diffs.Text1();
                    }

                    if (postcontext.Length != 0)
                    {
                        patch.Length1 += postcontext.Length;
                        patch.Length2 += postcontext.Length;
                        if (patch.Diffs.Count != 0
                            && patch.Diffs[patch.Diffs.Count - 1].Operation == Operation.Equal)
                        {
                            patch.Diffs[patch.Diffs.Count - 1] = patch.Diffs[patch.Diffs.Count - 1].Replace(patch.Diffs[patch.Diffs.Count - 1].Text + postcontext);
                        }
                        else
                        {
                            patch.Diffs.Add(Diff.Equal(postcontext));
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