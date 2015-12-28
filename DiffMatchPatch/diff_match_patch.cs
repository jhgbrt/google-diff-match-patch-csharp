using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using static DiffMatchPatch.TextUtil;

namespace DiffMatchPatch
{
    public class diff_match_patch
    {
        // Defaults.
        // Set these on your diff_match_patch instance to override the defaults.

        // Number of seconds to map a diff before giving up (0 for infinity).
        public float DiffTimeout = 1.0f;
        // Cost of an empty edit operation in terms of edit characters.
        public short DiffEditCost = 4;
        // At what point is no match declared (0.0 = perfection, 1.0 = very loose).
        public float MatchThreshold = 0.5f;
        // How far to search for a match (0 = exact location, 1000+ = broad match).
        // A match this many characters away from the expected location will add
        // 1.0 to the score (0.0 is a perfect match).
        public int MatchDistance = 1000;
        // When deleting a large block of text (over ~64 characters), how close
        // do the contents have to be to match the expected contents. (0.0 =
        // perfection, 1.0 = very loose).  Note that Match_Threshold controls
        // how closely the end points of a delete need to match.
        public float PatchDeleteThreshold = 0.5f;
        // Chunk size for context length.
        public short PatchMargin = 4;

        // The number of bits in an int.
        private short _matchMaxBits = 32;


        //  DIFF FUNCTIONS


        /**
         * Find the differences between two texts.
         * Run a faster, slightly less optimal diff.
         * This method allows the 'checklines' of diff_main() to be optional.
         * Most of the time checklines is wanted, so default to true.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @return List of Diff objects.
         */
        public List<Diff> diff_main(string text1, string text2)
        {
            return diff_main(text1, text2, true);
        }

        /**
         * Find the differences between two texts.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param checklines Speedup flag.  If false, then don't run a
         *     line-level diff first to identify the changed areas.
         *     If true, then run a faster slightly less optimal diff.
         * @return List of Diff objects.
         */
        public List<Diff> diff_main(string text1, string text2, bool checklines)
        {
            // Set a deadline by which time the diff must be complete.
            DateTime deadline;
            if (DiffTimeout <= 0)
            {
                deadline = DateTime.MaxValue;
            }
            else
            {
                deadline = DateTime.Now +
                           new TimeSpan(((long)(DiffTimeout * 1000)) * 10000);
            }
            return diff_main(text1, text2, checklines, deadline);
        }

        /**
         * Find the differences between two texts.  Simplifies the problem by
         * stripping any common prefix or suffix off the texts before diffing.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param checklines Speedup flag.  If false, then don't run a
         *     line-level diff first to identify the changed areas.
         *     If true, then run a faster slightly less optimal diff.
         * @param deadline Time when the diff should be complete by.  Used
         *     internally for recursive calls.  Users should set DiffTimeout
         *     instead.
         * @return List of Diff objects.
         */
        private List<Diff> diff_main(string text1, string text2, bool checklines,
            DateTime deadline)
        {
            // Check for null inputs not needed since null can't be passed in C#.

            // Check for equality (speedup).
            List<Diff> diffs;
            if (text1 == text2)
            {
                diffs = new List<Diff>();
                if (text1.Length != 0)
                {
                    diffs.Add(Diff.EQUAL(text1));
                }
                return diffs;
            }

            // Trim off common prefix (speedup).
            int commonlength = CommonPrefix(text1, text2);
            string commonprefix = text1.Substring(0, commonlength);
            text1 = text1.Substring(commonlength);
            text2 = text2.Substring(commonlength);

            // Trim off common suffix (speedup).
            commonlength = CommonSuffix(text1, text2);
            string commonsuffix = text1.Substring(text1.Length - commonlength);
            text1 = text1.Substring(0, text1.Length - commonlength);
            text2 = text2.Substring(0, text2.Length - commonlength);

            // Compute the diff on the middle block.
            diffs = diff_compute(text1, text2, checklines, deadline);

            // Restore the prefix and suffix.
            if (commonprefix.Length != 0)
            {
                diffs.Insert(0, (Diff.EQUAL(commonprefix)));
            }
            if (commonsuffix.Length != 0)
            {
                diffs.Add(Diff.EQUAL(commonsuffix));
            }

            diff_cleanupMerge(diffs);
            return diffs;
        }

        /**
         * Find the differences between two texts.  Assumes that the texts do not
         * have any common prefix or suffix.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param checklines Speedup flag.  If false, then don't run a
         *     line-level diff first to identify the changed areas.
         *     If true, then run a faster slightly less optimal diff.
         * @param deadline Time when the diff should be complete by.
         * @return List of Diff objects.
         */
        private List<Diff> diff_compute(string text1, string text2,
            bool checklines, DateTime deadline)
        {
            List<Diff> diffs = new List<Diff>();

            if (text1.Length == 0)
            {
                // Just add some text (speedup).
                diffs.Add(Diff.INSERT(text2));
                return diffs;
            }

            if (text2.Length == 0)
            {
                // Just delete some text (speedup).
                diffs.Add(Diff.DELETE(text1));
                return diffs;
            }

            string longtext = text1.Length > text2.Length ? text1 : text2;
            string shorttext = text1.Length > text2.Length ? text2 : text1;
            int i = longtext.IndexOf(shorttext, StringComparison.Ordinal);
            if (i != -1)
            {
                // Shorter text is inside the longer text (speedup).
                Operation op = text1.Length > text2.Length ? Operation.DELETE : Operation.INSERT;
                diffs.Add(Diff.Create(op, longtext.Substring(0, i)));
                diffs.Add(Diff.EQUAL(shorttext));
                diffs.Add(Diff.Create(op, longtext.Substring(i + shorttext.Length)));
                return diffs;
            }

            if (shorttext.Length == 1)
            {
                // Single character string.
                // After the previous speedup, the character can't be an equality.
                diffs.Add(Diff.DELETE(text1));
                diffs.Add(Diff.INSERT(text2));
                return diffs;
            }

            // Don't risk returning a non-optimal diff if we have unlimited time.
            if (DiffTimeout > 0)
            {
                // Check to see if the problem can be split in two.
                string[] hm = HalfMatch(text1, text2);
                if (hm != null)
                {
                    // A half-match was found, sort out the return data.
                    string text1A = hm[0];
                    string text1B = hm[1];
                    string text2A = hm[2];
                    string text2B = hm[3];
                    string midCommon = hm[4];
                    // Send both pairs off for separate processing.
                    List<Diff> diffsA = diff_main(text1A, text2A, checklines, deadline);
                    List<Diff> diffsB = diff_main(text1B, text2B, checklines, deadline);
                    // Merge the results.
                    diffs = diffsA;
                    diffs.Add(Diff.EQUAL(midCommon));
                    diffs.AddRange(diffsB);
                    return diffs;
                }
            }
            if (checklines && text1.Length > 100 && text2.Length > 100)
            {
                return diff_lineMode(text1, text2, deadline);
            }

            return diff_bisect(text1, text2, deadline);
        }

        /**
         * Do a quick line-level diff on both strings, then rediff the parts for
         * greater accuracy.
         * This speedup can produce non-minimal diffs.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param deadline Time when the diff should be complete by.
         * @return List of Diff objects.
         */
        private List<Diff> diff_lineMode(string text1, string text2, DateTime deadline)
        {
            // Scan the text on a line-by-line basis first.
            var b = LinesToChars(text1, text2);
            text1 = b.Item1;
            text2 = b.Item2;
            List<string> linearray = b.Item3;

            List<Diff> diffs = diff_main(text1, text2, false, deadline);

            // Convert the diff back to original text.
            diffs = diff_charsToLines(diffs, linearray).ToList();
            // Eliminate freak matches (e.g. blank lines)
            diff_cleanupSemantic(diffs);

            // Rediff any replacement blocks, this time character-by-character.
            // Add a dummy entry at the end.
            diffs.Add(Diff.EQUAL(string.Empty));
            int pointer = 0;
            int countDelete = 0;
            int countInsert = 0;
            string textDelete = string.Empty;
            string textInsert = string.Empty;
            while (pointer < diffs.Count)
            {
                switch (diffs[pointer].operation)
                {
                    case Operation.INSERT:
                        countInsert++;
                        textInsert += diffs[pointer].text;
                        break;
                    case Operation.DELETE:
                        countDelete++;
                        textDelete += diffs[pointer].text;
                        break;
                    case Operation.EQUAL:
                        // Upon reaching an equality, check for prior redundancies.
                        if (countDelete >= 1 && countInsert >= 1)
                        {
                            // Delete the offending records and add the merged ones.
                            var a = diff_main(textDelete, textInsert, false, deadline);
                            var count = countDelete + countInsert;
                            var index = pointer - count;
                            diffs.Splice(index, count, a);
                            pointer = index + a.Count;
                        }
                        countInsert = 0;
                        countDelete = 0;
                        textDelete = string.Empty;
                        textInsert = string.Empty;
                        break;
                }
                pointer++;
            }
            diffs.RemoveAt(diffs.Count - 1);  // Remove the dummy entry at the end.

            return diffs;
        }

        /**
         * Find the 'middle snake' of a diff, split the problem in two
         * and return the recursively constructed diff.
         * See Myers 1986 paper: An O(ND) Difference Algorithm and Its Variations.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param deadline Time at which to bail if not yet complete.
         * @return List of Diff objects.
         */
        protected List<Diff> diff_bisect(string text1, string text2, DateTime deadline)
        {
            // Cache the text lengths to prevent multiple calls.
            int text1Length = text1.Length;
            int text2Length = text2.Length;
            int maxD = (text1Length + text2Length + 1) / 2;
            int vOffset = maxD;
            int vLength = 2 * maxD;
            int[] v1 = new int[vLength];
            int[] v2 = new int[vLength];
            for (int x = 0; x < vLength; x++)
            {
                v1[x] = -1;
                v2[x] = -1;
            }
            v1[vOffset + 1] = 0;
            v2[vOffset + 1] = 0;
            int delta = text1Length - text2Length;
            // If the total number of characters is odd, then the front path will
            // collide with the reverse path.
            bool front = (delta % 2 != 0);
            // Offsets for start and end of k loop.
            // Prevents mapping of space beyond the grid.
            int k1Start = 0;
            int k1End = 0;
            int k2Start = 0;
            int k2End = 0;
            for (int d = 0; d < maxD; d++)
            {
                // Bail out if deadline is reached.
                if (DateTime.Now > deadline)
                {
                    break;
                }

                // Walk the front path one step.
                for (int k1 = -d + k1Start; k1 <= d - k1End; k1 += 2)
                {
                    int k1Offset = vOffset + k1;
                    int x1;
                    if (k1 == -d || k1 != d && v1[k1Offset - 1] < v1[k1Offset + 1])
                    {
                        x1 = v1[k1Offset + 1];
                    }
                    else
                    {
                        x1 = v1[k1Offset - 1] + 1;
                    }
                    int y1 = x1 - k1;
                    while (x1 < text1Length && y1 < text2Length
                           && text1[x1] == text2[y1])
                    {
                        x1++;
                        y1++;
                    }
                    v1[k1Offset] = x1;
                    if (x1 > text1Length)
                    {
                        // Ran off the right of the graph.
                        k1End += 2;
                    }
                    else if (y1 > text2Length)
                    {
                        // Ran off the bottom of the graph.
                        k1Start += 2;
                    }
                    else if (front)
                    {
                        int k2Offset = vOffset + delta - k1;
                        if (k2Offset >= 0 && k2Offset < vLength && v2[k2Offset] != -1)
                        {
                            // Mirror x2 onto top-left coordinate system.
                            int x2 = text1Length - v2[k2Offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return diff_bisectSplit(text1, text2, x1, y1, deadline);
                            }
                        }
                    }
                }

                // Walk the reverse path one step.
                for (int k2 = -d + k2Start; k2 <= d - k2End; k2 += 2)
                {
                    int k2Offset = vOffset + k2;
                    int x2;
                    if (k2 == -d || k2 != d && v2[k2Offset - 1] < v2[k2Offset + 1])
                    {
                        x2 = v2[k2Offset + 1];
                    }
                    else
                    {
                        x2 = v2[k2Offset - 1] + 1;
                    }
                    int y2 = x2 - k2;
                    while (x2 < text1Length && y2 < text2Length
                           && text1[text1Length - x2 - 1]
                           == text2[text2Length - y2 - 1])
                    {
                        x2++;
                        y2++;
                    }
                    v2[k2Offset] = x2;
                    if (x2 > text1Length)
                    {
                        // Ran off the left of the graph.
                        k2End += 2;
                    }
                    else if (y2 > text2Length)
                    {
                        // Ran off the top of the graph.
                        k2Start += 2;
                    }
                    else if (!front)
                    {
                        int k1Offset = vOffset + delta - k2;
                        if (k1Offset >= 0 && k1Offset < vLength && v1[k1Offset] != -1)
                        {
                            int x1 = v1[k1Offset];
                            int y1 = vOffset + x1 - k1Offset;
                            // Mirror x2 onto top-left coordinate system.
                            x2 = text1Length - v2[k2Offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return diff_bisectSplit(text1, text2, x1, y1, deadline);
                            }
                        }
                    }
                }
            }
            // Diff took too long and hit the deadline or
            // number of diffs equals number of characters, no commonality at all.
            var diffs = new List<Diff> {Diff.DELETE(text1), Diff.INSERT(text2)};
            return diffs;
        }

        /**
         * Given the location of the 'middle snake', split the diff in two parts
         * and recurse.
         * @param text1 Old string to be diffed.
         * @param text2 New string to be diffed.
         * @param x Index of split point in text1.
         * @param y Index of split point in text2.
         * @param deadline Time at which to bail if not yet complete.
         * @return LinkedList of Diff objects.
         */
        private List<Diff> diff_bisectSplit(string text1, string text2, int x, int y, DateTime deadline)
        {
            string text1A = text1.Substring(0, x);
            string text2A = text2.Substring(0, y);
            string text1B = text1.Substring(x);
            string text2B = text2.Substring(y);

            // Compute both diffs serially.
            List<Diff> diffs = diff_main(text1A, text2A, false, deadline);
            List<Diff> diffsb = diff_main(text1B, text2B, false, deadline);

            diffs.AddRange(diffsb);
            return diffs;
        }

        /**
         * Rehydrate the text in a diff from a string of line hashes to real lines
         * of text.
         * @param diffs List of Diff objects.
         * @param lineArray List of unique strings.
         */
        protected IEnumerable<Diff> diff_charsToLines(ICollection<Diff> diffs, List<string> lineArray)
        {
            StringBuilder text;
            foreach (Diff diff in diffs)
            {
                text = new StringBuilder();
                foreach (char c in diff.text)
                {
                    text.Append(lineArray[c]);
                }
                yield return diff.Replace(text.ToString());
            }
        }



        /**
         * Reduce the number of edits by eliminating semantically trivial
         * equalities.
         * @param diffs List of Diff objects.
         */
        public void diff_cleanupSemantic(List<Diff> diffs)
        {
            // Stack of indices where equalities are found.
            Stack<int> equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            string lastequality = null;
            int pointer = 0;  // Index of current position.
            // Number of characters that changed prior to the equality.
            int lengthInsertions1 = 0;
            int lengthDeletions1 = 0;
            // Number of characters that changed after the equality.
            int lengthInsertions2 = 0;
            int lengthDeletions2 = 0;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer].operation == Operation.EQUAL)
                {  // Equality found.
                    equalities.Push(pointer);
                    lengthInsertions1 = lengthInsertions2;
                    lengthDeletions1 = lengthDeletions2;
                    lengthInsertions2 = 0;
                    lengthDeletions2 = 0;
                    lastequality = diffs[pointer].text;
                }
                else
                {  // an insertion or deletion
                    if (diffs[pointer].operation == Operation.INSERT)
                    {
                        lengthInsertions2 += diffs[pointer].text.Length;
                    }
                    else
                    {
                        lengthDeletions2 += diffs[pointer].text.Length;
                    }
                    // Eliminate an equality that is smaller or equal to the edits on both
                    // sides of it.
                    if (lastequality != null && (lastequality.Length
                                                 <= Math.Max(lengthInsertions1, lengthDeletions1))
                        && (lastequality.Length
                            <= Math.Max(lengthInsertions2, lengthDeletions2)))
                    {
                        // Duplicate record.

                        diffs.Splice(equalities.Peek(), 1, Diff.DELETE(lastequality), Diff.INSERT(lastequality));

                        // Throw away the equality we just deleted.
                        equalities.Pop();
                        if (equalities.Count > 0)
                        {
                            equalities.Pop();
                        }
                        pointer = equalities.Count > 0 ? equalities.Peek() : -1;
                        lengthInsertions1 = 0;  // Reset the counters.
                        lengthDeletions1 = 0;
                        lengthInsertions2 = 0;
                        lengthDeletions2 = 0;
                        lastequality = null;
                    }
                }
                pointer++;
            }

            diff_cleanupMerge(diffs);
            diff_cleanupSemanticLossless(diffs);

            // Find any overlaps between deletions and insertions.
            // e.g: <del>abcxxx</del><ins>xxxdef</ins>
            //   -> <del>abc</del>xxx<ins>def</ins>
            // e.g: <del>xxxabc</del><ins>defxxx</ins>
            //   -> <ins>def</ins>xxx<del>abc</del>
            // Only extract an overlap if it is as big as the edit ahead or behind it.
            pointer = 1;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer - 1].operation == Operation.DELETE &&
                    diffs[pointer].operation == Operation.INSERT)
                {
                    string deletion = diffs[pointer - 1].text;
                    string insertion = diffs[pointer].text;
                    int overlapLength1 = CommonOverlap(deletion, insertion);
                    int overlapLength2 = CommonOverlap(insertion, deletion);
                    if (overlapLength1 >= overlapLength2)
                    {
                        if (overlapLength1 >= deletion.Length / 2.0 ||
                            overlapLength1 >= insertion.Length / 2.0)
                        {
                            // Overlap found.
                            // Insert an equality and trim the surrounding edits.
                            var newDiffs = new[]
                            {
                                Diff.DELETE(deletion.Substring(0, deletion.Length - overlapLength1)),
                                Diff.EQUAL(insertion.Substring(0, overlapLength1)),
                                Diff.INSERT(insertion.Substring(overlapLength1))
                            };

                            diffs.Splice(pointer -1, 2, newDiffs);
                            pointer++;
                        }
                    }
                    else
                    {
                        if (overlapLength2 >= deletion.Length / 2.0 ||
                            overlapLength2 >= insertion.Length / 2.0)
                        {
                            // Reverse overlap found.
                            // Insert an equality and swap and trim the surrounding edits.

                            diffs.Splice(pointer - 1, 2,
                                Diff.INSERT(insertion.Substring(0, insertion.Length - overlapLength2)),
                                Diff.EQUAL(deletion.Substring(0, overlapLength2)),
                                Diff.DELETE(deletion.Substring(overlapLength2)
                                    ));
                            pointer++;
                        }
                    }
                    pointer++;
                }
                pointer++;
            }
        }

        /**
         * Look for single edits surrounded on both sides by equalities
         * which can be shifted sideways to align the edit to a word boundary.
         * e.g: The c<ins>at c</ins>ame. -> The <ins>cat </ins>came.
         * @param diffs List of Diff objects.
         */
        public void diff_cleanupSemanticLossless(List<Diff> diffs)
        {
            int pointer = 1;
            // Intentionally ignore the first and last element (don't need checking).
            while (pointer < diffs.Count - 1)
            {
                if (diffs[pointer - 1].operation == Operation.EQUAL &&
                    diffs[pointer + 1].operation == Operation.EQUAL)
                {
                    // This is a single edit surrounded by equalities.
                    string equality1 = diffs[pointer - 1].text;
                    string edit = diffs[pointer].text;
                    string equality2 = diffs[pointer + 1].text;

                    // First, shift the edit as far left as possible.
                    int commonOffset = CommonSuffix(equality1, edit);
                    if (commonOffset > 0)
                    {
                        string commonString = edit.Substring(edit.Length - commonOffset);
                        equality1 = equality1.Substring(0, equality1.Length - commonOffset);
                        edit = commonString + edit.Substring(0, edit.Length - commonOffset);
                        equality2 = commonString + equality2;
                    }

                    // Second, step character by character right,
                    // looking for the best fit.
                    string bestEquality1 = equality1;
                    string bestEdit = edit;
                    string bestEquality2 = equality2;
                    int bestScore = diff_cleanupSemanticScore(equality1, edit) + diff_cleanupSemanticScore(edit, equality2);
                    while (edit.Length != 0 && equality2.Length != 0 && edit[0] == equality2[0])
                    {
                        equality1 += edit[0];
                        edit = edit.Substring(1) + equality2[0];
                        equality2 = equality2.Substring(1);
                        int score = diff_cleanupSemanticScore(equality1, edit) + diff_cleanupSemanticScore(edit, equality2);
                        // The >= encourages trailing rather than leading whitespace on
                        // edits.
                        if (score >= bestScore)
                        {
                            bestScore = score;
                            bestEquality1 = equality1;
                            bestEdit = edit;
                            bestEquality2 = equality2;
                        }
                    }

                    if (diffs[pointer - 1].text != bestEquality1)
                    {
                        // We have an improvement, save it back to the diff.

                        var newDiffs = new[]
                        {
                            Diff.EQUAL(bestEquality1),
                            diffs[pointer].Replace(bestEdit),
                            Diff.EQUAL(bestEquality2)
                        }.Where(d => !string.IsNullOrEmpty(d.text))
                            .ToArray();

                        diffs.Splice(pointer - 1, 3, newDiffs);
                        pointer = pointer - (3 - newDiffs.Length);
                    }
                }
                pointer++;
            }
        }

        /**
         * Given two strings, comAdde a score representing whether the internal
         * boundary falls on logical boundaries.
         * Scores range from 6 (best) to 0 (worst).
         * @param one First string.
         * @param two Second string.
         * @return The score.
         */
        private int diff_cleanupSemanticScore(string one, string two)
        {
            if (one.Length == 0 || two.Length == 0)
            {
                // Edges are the best.
                return 6;
            }

            // Each port of this function behaves slightly differently due to
            // subtle differences in each language's definition of things like
            // 'whitespace'.  Since this function's purpose is largely cosmetic,
            // the choice has been made to use each language's native features
            // rather than force total conformity.
            char char1 = one[one.Length - 1];
            char char2 = two[0];
            bool nonAlphaNumeric1 = !Char.IsLetterOrDigit(char1);
            bool nonAlphaNumeric2 = !Char.IsLetterOrDigit(char2);
            bool whitespace1 = nonAlphaNumeric1 && Char.IsWhiteSpace(char1);
            bool whitespace2 = nonAlphaNumeric2 && Char.IsWhiteSpace(char2);
            bool lineBreak1 = whitespace1 && Char.IsControl(char1);
            bool lineBreak2 = whitespace2 && Char.IsControl(char2);
            bool blankLine1 = lineBreak1 && _blanklineend.IsMatch(one);
            bool blankLine2 = lineBreak2 && _blanklinestart.IsMatch(two);

            if (blankLine1 || blankLine2)
            {
                // Five points for blank lines.
                return 5;
            }
            if (lineBreak1 || lineBreak2)
            {
                // Four points for line breaks.
                return 4;
            }
            if (nonAlphaNumeric1 && !whitespace1 && whitespace2)
            {
                // Three points for end of sentences.
                return 3;
            }
            if (whitespace1 || whitespace2)
            {
                // Two points for whitespace.
                return 2;
            }
            if (nonAlphaNumeric1 || nonAlphaNumeric2)
            {
                // One point for non-alphanumeric.
                return 1;
            }
            return 0;
        }

        // Define some regex patterns for matching boundaries.
        private Regex _blanklineend = new Regex("\\n\\r?\\n\\Z");
        private Regex _blanklinestart = new Regex("\\A\\r?\\n\\r?\\n");

        /**
         * Reduce the number of edits by eliminating operationally trivial
         * equalities.
         * @param diffs List of Diff objects.
         */
        public void diff_cleanupEfficiency(List<Diff> diffs)
        {
            bool changes = false;
            // Stack of indices where equalities are found.
            Stack<int> equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            string lastequality = string.Empty;
            int pointer = 0;  // Index of current position.
            // Is there an insertion operation before the last equality.
            bool preIns = false;
            // Is there a deletion operation before the last equality.
            bool preDel = false;
            // Is there an insertion operation after the last equality.
            bool postIns = false;
            // Is there a deletion operation after the last equality.
            bool postDel = false;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer].operation == Operation.EQUAL)
                {  // Equality found.
                    if (diffs[pointer].text.Length < DiffEditCost
                        && (postIns || postDel))
                    {
                        // Candidate found.
                        equalities.Push(pointer);
                        preIns = postIns;
                        preDel = postDel;
                        lastequality = diffs[pointer].text;
                    }
                    else
                    {
                        // Not a candidate, and can never become one.
                        equalities.Clear();
                        lastequality = string.Empty;
                    }
                    postIns = postDel = false;
                }
                else
                {  // An insertion or deletion.
                    if (diffs[pointer].operation == Operation.DELETE)
                    {
                        postDel = true;
                    }
                    else
                    {
                        postIns = true;
                    }
                    /*
                     * Five types to be split:
                     * <ins>A</ins><del>B</del>XY<ins>C</ins><del>D</del>
                     * <ins>A</ins>X<ins>C</ins><del>D</del>
                     * <ins>A</ins><del>B</del>X<ins>C</ins>
                     * <ins>A</del>X<ins>C</ins><del>D</del>
                     * <ins>A</ins><del>B</del>X<del>C</del>
                     */
                    if ((lastequality.Length != 0)
                        && ((preIns && preDel && postIns && postDel)
                            || ((lastequality.Length < DiffEditCost / 2)
                                && ((preIns ? 1 : 0) + (preDel ? 1 : 0) + (postIns ? 1 : 0)
                                    + (postDel ? 1 : 0)) == 3)))
                    {
                        diffs.Splice(equalities.Peek(), 1, Diff.DELETE(lastequality), Diff.INSERT(lastequality));
                        equalities.Pop();  // Throw away the equality we just deleted.
                        lastequality = string.Empty;
                        if (preIns && preDel)
                        {
                            // No changes made which could affect previous entry, keep going.
                            postIns = postDel = true;
                            equalities.Clear();
                        }
                        else
                        {
                            if (equalities.Count > 0)
                            {
                                equalities.Pop();
                            }

                            pointer = equalities.Count > 0 ? equalities.Peek() : -1;
                            postIns = postDel = false;
                        }
                        changes = true;
                    }
                }
                pointer++;
            }

            if (changes)
            {
                diff_cleanupMerge(diffs);
            }
        }

        /**
         * Reorder and merge like edit sections.  Merge equalities.
         * Any edit section can move as long as it doesn't cross an equality.
         * @param diffs List of Diff objects.
         */
        public static void diff_cleanupMerge(List<Diff> diffs)
        {
            // Add a dummy entry at the end.
            diffs.Add(Diff.EQUAL(string.Empty));
            int countDelete = 0;
            int countInsert = 0;
            string textDelete = string.Empty;
            string textInsert = string.Empty;
            int commonlength;

            int pointer = 0;
            while (pointer < diffs.Count)
            {
                switch (diffs[pointer].operation)
                {
                    case Operation.INSERT:
                        countInsert++;
                        textInsert += diffs[pointer].text;
                        pointer++;
                        break;
                    case Operation.DELETE:
                        countDelete++;
                        textDelete += diffs[pointer].text;
                        pointer++;
                        break;
                    case Operation.EQUAL:
                        // Upon reaching an equality, check for prior redundancies.
                        if (countDelete + countInsert > 1)
                        {
                            if (countDelete != 0 && countInsert != 0)
                            {
                                // Factor out any common prefixies.
                                commonlength = CommonPrefix(textInsert, textDelete);
                                if (commonlength != 0)
                                {
                                    var index = pointer - countDelete - countInsert - 1;
                                    if (index >= 0 && diffs[index].operation == Operation.EQUAL)
                                    {
                                        diffs[index] = diffs[index].Replace(diffs[index].text + textInsert.Substring(0, commonlength));
                                    }
                                    else
                                    {
                                        diffs.Insert(0, Diff.EQUAL(textInsert.Substring(0, commonlength)));
                                        pointer++;
                                    }
                                    textInsert = textInsert.Substring(commonlength);
                                    textDelete = textDelete.Substring(commonlength);
                                }
                                // Factor out any common suffixies.
                                commonlength = CommonSuffix(textInsert, textDelete);
                                if (commonlength != 0)
                                {
                                    diffs[pointer] = diffs[pointer].Replace(textInsert.Substring(textInsert.Length
                                                                                                  - commonlength) + diffs[pointer].text);
                                    textInsert = textInsert.Substring(0, textInsert.Length
                                                                           - commonlength);
                                    textDelete = textDelete.Substring(0, textDelete.Length
                                                                           - commonlength);
                                }
                            }
                            // Delete the offending records and add the merged ones.
                            if (countDelete == 0)
                            {
                                diffs.Splice(pointer - countInsert,
                                    countDelete + countInsert,
                                    Diff.INSERT(textInsert));
                            }
                            else if (countInsert == 0)
                            {
                                diffs.Splice(pointer - countDelete,
                                    countDelete + countInsert,
                                    Diff.DELETE(textDelete));
                            }
                            else
                            {
                                diffs.Splice(pointer - countDelete - countInsert,
                                    countDelete + countInsert,
                                    Diff.DELETE(textDelete),
                                    Diff.INSERT(textInsert));
                            }
                            pointer = pointer - countDelete - countInsert +
                                      (countDelete != 0 ? 1 : 0) + (countInsert != 0 ? 1 : 0) + 1;
                        }
                        else if (pointer != 0
                                 && diffs[pointer - 1].operation == Operation.EQUAL)
                        {
                            // Merge this equality with the previous one.
                            diffs[pointer-1] = diffs[pointer-1].Replace(diffs[pointer-1].text + diffs[pointer].text);
                            diffs.RemoveAt(pointer);
                        }
                        else
                        {
                            pointer++;
                        }
                        countInsert = 0;
                        countDelete = 0;
                        textDelete = string.Empty;
                        textInsert = string.Empty;
                        break;
                }
            }
            if (diffs[diffs.Count - 1].text.Length == 0)
            {
                diffs.RemoveAt(diffs.Count - 1);  // Remove the dummy entry at the end.
            }

            // Second pass: look for single edits surrounded on both sides by
            // equalities which can be shifted sideways to eliminate an equality.
            // e.g: A<ins>BA</ins>C -> <ins>AB</ins>AC
            bool changes = false;
            pointer = 1;
            // Intentionally ignore the first and last element (don't need checking).
            while (pointer < (diffs.Count - 1))
            {
                if (diffs[pointer - 1].operation == Operation.EQUAL &&
                    diffs[pointer + 1].operation == Operation.EQUAL)
                {
                    // This is a single edit surrounded by equalities.
                    if (diffs[pointer].text.EndsWith(diffs[pointer - 1].text,
                        StringComparison.Ordinal))
                    {
                        // Shift the edit over the previous equality.
                        var text = diffs[pointer - 1].text +
                                   diffs[pointer].text.Substring(0, diffs[pointer].text.Length -
                                                                    diffs[pointer - 1].text.Length);
                        diffs[pointer] = diffs[pointer].Replace(text);
                        diffs[pointer + 1] = diffs[pointer + 1].Replace(diffs[pointer - 1].text
                                                                        + diffs[pointer + 1].text);
                        diffs.Splice(pointer - 1, 1);
                        changes = true;
                    }
                    else if (diffs[pointer].text.StartsWith(diffs[pointer + 1].text,
                        StringComparison.Ordinal))
                    {
                        // Shift the edit over the next equality.
                        diffs[pointer - 1] = diffs[pointer-1].Replace(diffs[pointer - 1].text + diffs[pointer + 1].text);
                        diffs[pointer] = diffs[pointer].Replace(diffs[pointer].text.Substring(diffs[pointer + 1].text.Length)
                                                                + diffs[pointer + 1].text);
                        diffs.Splice(pointer + 1, 1);
                        changes = true;
                    }
                }
                pointer++;
            }
            // If shifts were made, the diff needs reordering and another shift sweep.
            if (changes)
            {
                diff_cleanupMerge(diffs);
            }
        }

        /**
         * loc is a location in text1, comAdde and return the equivalent location in
         * text2.
         * e.g. "The cat" vs "The big cat", 1->1, 5->8
         * @param diffs List of Diff objects.
         * @param loc Location within text1.
         * @return Location within text2.
         */
        public int diff_xIndex(List<Diff> diffs, int loc)
        {
            int chars1 = 0;
            int chars2 = 0;
            int lastChars1 = 0;
            int lastChars2 = 0;
            Diff lastDiff = null;
            foreach (Diff aDiff in diffs)
            {
                if (aDiff.operation != Operation.INSERT)
                {
                    // Equality or deletion.
                    chars1 += aDiff.text.Length;
                }
                if (aDiff.operation != Operation.DELETE)
                {
                    // Equality or insertion.
                    chars2 += aDiff.text.Length;
                }
                if (chars1 > loc)
                {
                    // Overshot the location.
                    lastDiff = aDiff;
                    break;
                }
                lastChars1 = chars1;
                lastChars2 = chars2;
            }
            if (lastDiff != null && lastDiff.operation == Operation.DELETE)
            {
                // The location was deleted.
                return lastChars2;
            }
            // Add the remaining character length.
            return lastChars2 + (loc - lastChars1);
        }

        /**
         * Convert a Diff list into a pretty HTML report.
         * @param diffs List of Diff objects.
         * @return HTML representation.
         */
        public string diff_prettyHtml(List<Diff> diffs)
        {
            StringBuilder html = new StringBuilder();
            foreach (Diff aDiff in diffs)
            {
                string text = aDiff.text.Replace("&", "&amp;").Replace("<", "&lt;")
                    .Replace(">", "&gt;").Replace("\n", "&para;<br>");
                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        html.Append("<ins style=\"background:#e6ffe6;\">").Append(text)
                            .Append("</ins>");
                        break;
                    case Operation.DELETE:
                        html.Append("<del style=\"background:#ffe6e6;\">").Append(text)
                            .Append("</del>");
                        break;
                    case Operation.EQUAL:
                        html.Append("<span>").Append(text).Append("</span>");
                        break;
                }
            }
            return html.ToString();
        }

        /**
         * Compute and return the source text (all equalities and deletions).
         * @param diffs List of Diff objects.
         * @return Source text.
         */
        public string diff_text1(List<Diff> diffs)
        {
            StringBuilder text = new StringBuilder();
            foreach (Diff aDiff in diffs)
            {
                if (aDiff.operation != Operation.INSERT)
                {
                    text.Append(aDiff.text);
                }
            }
            return text.ToString();
        }

        /**
         * Compute and return the destination text (all equalities and insertions).
         * @param diffs List of Diff objects.
         * @return Destination text.
         */
        public string diff_text2(List<Diff> diffs)
        {
            StringBuilder text = new StringBuilder();
            foreach (Diff aDiff in diffs)
            {
                if (aDiff.operation != Operation.DELETE)
                {
                    text.Append(aDiff.text);
                }
            }
            return text.ToString();
        }

        /**
         * Compute the Levenshtein distance; the number of inserted, deleted or
         * substituted characters.
         * @param diffs List of Diff objects.
         * @return Number of changes.
         */
        public int diff_levenshtein(List<Diff> diffs)
        {
            int levenshtein = 0;
            int insertions = 0;
            int deletions = 0;
            foreach (Diff aDiff in diffs)
            {
                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        insertions += aDiff.text.Length;
                        break;
                    case Operation.DELETE:
                        deletions += aDiff.text.Length;
                        break;
                    case Operation.EQUAL:
                        // A deletion and an insertion is one substitution.
                        levenshtein += Math.Max(insertions, deletions);
                        insertions = 0;
                        deletions = 0;
                        break;
                }
            }
            levenshtein += Math.Max(insertions, deletions);
            return levenshtein;
        }

        /**
         * Crush the diff into an encoded string which describes the operations
         * required to transform text1 into text2.
         * E.g. =3\t-2\t+ing  -> Keep 3 chars, delete 2 chars, insert 'ing'.
         * Operations are tab-separated.  Inserted text is escaped using %xx
         * notation.
         * @param diffs Array of Diff objects.
         * @return Delta text.
         */
        public string diff_toDelta(List<Diff> diffs)
        {
            StringBuilder text = new StringBuilder();
            foreach (Diff aDiff in diffs)
            {
                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        text.Append("+").Append(HttpUtility.UrlEncode(aDiff.text,
                            new UTF8Encoding()).Replace('+', ' ')).Append("\t");
                        break;
                    case Operation.DELETE:
                        text.Append("-").Append(aDiff.text.Length).Append("\t");
                        break;
                    case Operation.EQUAL:
                        text.Append("=").Append(aDiff.text.Length).Append("\t");
                        break;
                }
            }
            string delta = text.ToString();
            if (delta.Length != 0)
            {
                // Strip off trailing tab character.
                delta = delta.Substring(0, delta.Length - 1);
                delta = UnescapeForEncodeUriCompatability(delta);
            }
            return delta;
        }

        /**
         * Given the original text1, and an encoded string which describes the
         * operations required to transform text1 into text2, comAdde the full diff.
         * @param text1 Source string for the diff.
         * @param delta Delta text.
         * @return Array of Diff objects or null if invalid.
         * @throws ArgumentException If invalid input.
         */
        public List<Diff> diff_fromDelta(string text1, string delta)
        {
            List<Diff> diffs = new List<Diff>();
            int pointer = 0;  // Cursor in text1
            string[] tokens = delta.Split(new[] { "\t" },
                StringSplitOptions.None);
            foreach (string token in tokens)
            {
                if (token.Length == 0)
                {
                    // Blank tokens are ok (from a trailing \t).
                    continue;
                }
                // Each token begins with a one character parameter which specifies the
                // operation of this token (delete, insert, equality).
                string param = token.Substring(1);
                switch (token[0])
                {
                    case '+':
                        // decode would change all "+" to " "
                        param = param.Replace("+", "%2b");

                        param = HttpUtility.UrlDecode(param, new UTF8Encoding(false, true));
                        //} catch (UnsupportedEncodingException e) {
                        //  // Not likely on modern system.
                        //  throw new Error("This system does not support UTF-8.", e);
                        //} catch (IllegalArgumentException e) {
                        //  // Malformed URI sequence.
                        //  throw new IllegalArgumentException(
                        //      "Illegal escape in diff_fromDelta: " + param, e);
                        //}
                        diffs.Add(Diff.INSERT(param));
                        break;
                    case '-':
                    // Fall through.
                    case '=':
                        int n;
                        try
                        {
                            n = Convert.ToInt32(param);
                        }
                        catch (FormatException e)
                        {
                            throw new ArgumentException(
                                "Invalid number in diff_fromDelta: " + param, e);
                        }
                        if (n < 0)
                        {
                            throw new ArgumentException(
                                "Negative number in diff_fromDelta: " + param);
                        }
                        string text;
                        try
                        {
                            text = text1.Substring(pointer, n);
                            pointer += n;
                        }
                        catch (ArgumentOutOfRangeException e)
                        {
                            throw new ArgumentException("Delta length (" + pointer
                                                        + ") larger than source text length (" + text1.Length
                                                        + ").", e);
                        }
                        if (token[0] == '=')
                        {
                            diffs.Add(Diff.EQUAL(text));
                        }
                        else
                        {
                            diffs.Add(Diff.DELETE(text));
                        }
                        break;
                    default:
                        // Anything else is an error.
                        throw new ArgumentException(
                            "Invalid diff operation in diff_fromDelta: " + token[0]);
                }
            }
            if (pointer != text1.Length)
            {
                throw new ArgumentException("Delta length (" + pointer
                                            + ") smaller than source text length (" + text1.Length + ").");
            }
            return diffs;
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
        public int match_main(string text, string pattern, int loc)
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
            return match_bitap(text, pattern, loc);
        }

        /**
         * Locate the best instance of 'pattern' in 'text' near 'loc' using the
         * Bitap algorithm.  Returns -1 if no match found.
         * @param text The text to search.
         * @param pattern The pattern to search for.
         * @param loc The location to search around.
         * @return Best match index or -1.
         */
        protected int match_bitap(string text, string pattern, int loc)
        {
            // assert (Match_MaxBits == 0 || pattern.Length <= Match_MaxBits)
            //    : "Pattern too long for this application.";

            // Initialise the alphabet.
            Dictionary<char, int> s = match_alphabet(pattern);

            // Highest score beyond which we give up.
            double scoreThreshold = MatchThreshold;
            // Is there a nearby exact match? (speedup)
            int bestLoc = text.IndexOf(pattern, loc, StringComparison.Ordinal);
            if (bestLoc != -1)
            {
                scoreThreshold = Math.Min(match_bitapScore(0, bestLoc, loc,
                    pattern), scoreThreshold);
                // What about in the other direction? (speedup)
                bestLoc = text.LastIndexOf(pattern,
                    Math.Min(loc + pattern.Length, text.Length),
                    StringComparison.Ordinal);
                if (bestLoc != -1)
                {
                    scoreThreshold = Math.Min(match_bitapScore(0, bestLoc, loc,
                        pattern), scoreThreshold);
                }
            }

            // Initialise the bit arrays.
            int matchmask = 1 << (pattern.Length - 1);
            bestLoc = -1;

            int binMin, binMid;
            int binMax = pattern.Length + text.Length;
            // Empty initialization added to appease C# compiler.
            int[] lastRd = new int[0];
            for (int d = 0; d < pattern.Length; d++)
            {
                // Scan for the best match; each iteration allows for one more error.
                // Run a binary search to determine how far from 'loc' we can stray at
                // this error level.
                binMin = 0;
                binMid = binMax;
                while (binMin < binMid)
                {
                    if (match_bitapScore(d, loc + binMid, loc, pattern)
                        <= scoreThreshold)
                    {
                        binMin = binMid;
                    }
                    else
                    {
                        binMax = binMid;
                    }
                    binMid = (binMax - binMin) / 2 + binMin;
                }
                // Use the result from this iteration as the maximum for the next.
                binMax = binMid;
                int start = Math.Max(1, loc - binMid + 1);
                int finish = Math.Min(loc + binMid, text.Length) + pattern.Length;

                int[] rd = new int[finish + 2];
                rd[finish + 1] = (1 << d) - 1;
                for (int j = finish; j >= start; j--)
                {
                    int charMatch;
                    if (text.Length <= j - 1 || !s.ContainsKey(text[j - 1]))
                    {
                        // Out of range.
                        charMatch = 0;
                    }
                    else
                    {
                        charMatch = s[text[j - 1]];
                    }
                    if (d == 0)
                    {
                        // First pass: exact match.
                        rd[j] = ((rd[j + 1] << 1) | 1) & charMatch;
                    }
                    else
                    {
                        // Subsequent passes: fuzzy match.
                        rd[j] = ((rd[j + 1] << 1) | 1) & charMatch
                                | (((lastRd[j + 1] | lastRd[j]) << 1) | 1) | lastRd[j + 1];
                    }
                    if ((rd[j] & matchmask) != 0)
                    {
                        double score = match_bitapScore(d, j - 1, loc, pattern);
                        // This match will almost certainly be better than any existing
                        // match.  But check anyway.
                        if (score <= scoreThreshold)
                        {
                            // Told you so.
                            scoreThreshold = score;
                            bestLoc = j - 1;
                            if (bestLoc > loc)
                            {
                                // When passing loc, don't exceed our current distance from loc.
                                start = Math.Max(1, 2 * loc - bestLoc);
                            }
                            else
                            {
                                // Already passed loc, downhill from here on in.
                                break;
                            }
                        }
                    }
                }
                if (match_bitapScore(d + 1, loc, loc, pattern) > scoreThreshold)
                {
                    // No hope for a (better) match at greater error levels.
                    break;
                }
                lastRd = rd;
            }
            return bestLoc;
        }

        /**
         * Compute and return the score for a match with e errors and x location.
         * @param e Number of errors in match.
         * @param x Location of match.
         * @param loc Expected location of match.
         * @param pattern Pattern being sought.
         * @return Overall score for match (0.0 = good, 1.0 = bad).
         */
        private double match_bitapScore(int e, int x, int loc, string pattern)
        {
            float accuracy = (float)e / pattern.Length;
            int proximity = Math.Abs(loc - x);
            if (MatchDistance == 0)
            {
                // Dodge divide by zero error.
                return proximity == 0 ? accuracy : 1.0;
            }
            return accuracy + (proximity / (float)MatchDistance);
        }

        /**
         * Initialise the alphabet for the Bitap algorithm.
         * @param pattern The text to encode.
         * @return Hash of character locations.
         */
        protected Dictionary<char, int> match_alphabet(string pattern)
        {
            Dictionary<char, int> s = new Dictionary<char, int>();
            char[] charPattern = pattern.ToCharArray();
            foreach (char c in charPattern)
            {
                if (!s.ContainsKey(c))
                {
                    s.Add(c, 0);
                }
            }
            int i = 0;
            foreach (char c in charPattern)
            {
                int value = s[c] | (1 << (pattern.Length - i - 1));
                s[c] = value;
                i++;
            }
            return s;
        }


        //  PATCH FUNCTIONS


        /**
         * Increase the context until it is unique,
         * but don't let the pattern expand beyond Match_MaxBits.
         * @param patch The patch to grow.
         * @param text Source text.
         */
        protected void patch_addContext(Patch patch, string text)
        {
            if (text.Length == 0)
            {
                return;
            }
            string pattern = text.Substring(patch.start2, patch.length1);
            int padding = 0;

            // Look for the first and last matches of pattern in text.  If two
            // different matches are found, increase the pattern length.
            while (text.IndexOf(pattern, StringComparison.Ordinal)
                   != text.LastIndexOf(pattern, StringComparison.Ordinal)
                   && pattern.Length < _matchMaxBits - PatchMargin - PatchMargin)
            {
                padding += PatchMargin;
                pattern = text.JavaSubstring(Math.Max(0, patch.start2 - padding),
                    Math.Min(text.Length, patch.start2 + patch.length1 + padding));
            }
            // Add one chunk for good luck.
            padding += PatchMargin;

            // Add the prefix.
            string prefix = text.JavaSubstring(Math.Max(0, patch.start2 - padding),
                patch.start2);
            if (prefix.Length != 0)
            {
                patch.diffs.Insert(0, Diff.EQUAL(prefix));
            }
            // Add the suffix.
            string suffix = text.JavaSubstring(patch.start2 + patch.length1,
                Math.Min(text.Length, patch.start2 + patch.length1 + padding));
            if (suffix.Length != 0)
            {
                patch.diffs.Add(Diff.EQUAL(suffix));
            }

            // Roll back the start points.
            patch.start1 -= prefix.Length;
            patch.start2 -= prefix.Length;
            // Extend the lengths.
            patch.length1 += prefix.Length + suffix.Length;
            patch.length2 += prefix.Length + suffix.Length;
        }

        /**
         * Compute a list of patches to turn text1 into text2.
         * A set of diffs will be computed.
         * @param text1 Old text.
         * @param text2 New text.
         * @return List of Patch objects.
         */
        public List<Patch> patch_make(string text1, string text2)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            // No diffs provided, comAdde our own.
            List<Diff> diffs = diff_main(text1, text2, true);
            if (diffs.Count > 2)
            {
                diff_cleanupSemantic(diffs);
                diff_cleanupEfficiency(diffs);
            }
            return patch_make(text1, diffs);
        }

        /**
         * Compute a list of patches to turn text1 into text2.
         * text1 will be derived from the provided diffs.
         * @param diffs Array of Diff objects for text1 to text2.
         * @return List of Patch objects.
         */
        public List<Patch> patch_make(List<Diff> diffs)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            // No origin string provided, comAdde our own.
            string text1 = diff_text1(diffs);
            return patch_make(text1, diffs);
        }

        /**
         * Compute a list of patches to turn text1 into text2.
         * text2 is ignored, diffs are the delta between text1 and text2.
         * @param text1 Old text
         * @param text2 Ignored.
         * @param diffs Array of Diff objects for text1 to text2.
         * @return List of Patch objects.
         * @deprecated Prefer patch_make(string text1, List<Diff> diffs).
         */
        public List<Patch> patch_make(string text1, string text2,
            List<Diff> diffs)
        {
            return patch_make(text1, diffs);
        }

        /**
         * Compute a list of patches to turn text1 into text2.
         * text2 is not provided, diffs are the delta between text1 and text2.
         * @param text1 Old text.
         * @param diffs Array of Diff objects for text1 to text2.
         * @return List of Patch objects.
         */
        public List<Patch> patch_make(string text1, List<Diff> diffs)
        {
            // Check for null inputs not needed since null can't be passed in C#.
            List<Patch> patches = new List<Patch>();
            if (diffs.Count == 0)
            {
                return patches;  // Get rid of the null case.
            }
            Patch patch = new Patch();
            int charCount1 = 0;  // Number of characters into the text1 string.
            int charCount2 = 0;  // Number of characters into the text2 string.
            // Start with text1 (prepatch_text) and apply the diffs until we arrive at
            // text2 (postpatch_text). We recreate the patches one by one to determine
            // context info.
            string prepatchText = text1;
            string postpatchText = text1;
            foreach (Diff aDiff in diffs)
            {
                if (patch.diffs.Count == 0 && aDiff.operation != Operation.EQUAL)
                {
                    // A new patch starts here.
                    patch.start1 = charCount1;
                    patch.start2 = charCount2;
                }

                switch (aDiff.operation)
                {
                    case Operation.INSERT:
                        patch.diffs.Add(aDiff);
                        patch.length2 += aDiff.text.Length;
                        postpatchText = postpatchText.Insert(charCount2, aDiff.text);
                        break;
                    case Operation.DELETE:
                        patch.length1 += aDiff.text.Length;
                        patch.diffs.Add(aDiff);
                        postpatchText = postpatchText.Remove(charCount2,
                            aDiff.text.Length);
                        break;
                    case Operation.EQUAL:
                        if (aDiff.text.Length <= 2 * PatchMargin
                            && patch.diffs.Count() != 0 && aDiff != diffs.Last())
                        {
                            // Small equality inside a patch.
                            patch.diffs.Add(aDiff);
                            patch.length1 += aDiff.text.Length;
                            patch.length2 += aDiff.text.Length;
                        }

                        if (aDiff.text.Length >= 2 * PatchMargin)
                        {
                            // Time for a new patch.
                            if (patch.diffs.Count != 0)
                            {
                                patch_addContext(patch, prepatchText);
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
                if (aDiff.operation != Operation.INSERT)
                {
                    charCount1 += aDiff.text.Length;
                }
                if (aDiff.operation != Operation.DELETE)
                {
                    charCount2 += aDiff.text.Length;
                }
            }
            // Pick up the leftover patch if not empty.
            if (patch.diffs.Count != 0)
            {
                patch_addContext(patch, prepatchText);
                patches.Add(patch);
            }

            return patches;
        }

        /**
         * Given an array of patches, return another array that is identical.
         * @param patches Array of Patch objects.
         * @return Array of Patch objects.
         */
        public List<Patch> patch_deepCopy(List<Patch> patches)
        {
            List<Patch> patchesCopy = new List<Patch>();
            foreach (Patch aPatch in patches)
            {
                Patch patchCopy = new Patch();
                foreach (Diff aDiff in aPatch.diffs)
                {
                    Diff diffCopy = aDiff.Copy();
                    patchCopy.diffs.Add(diffCopy);
                }
                patchCopy.start1 = aPatch.start1;
                patchCopy.start2 = aPatch.start2;
                patchCopy.length1 = aPatch.length1;
                patchCopy.length2 = aPatch.length2;
                patchesCopy.Add(patchCopy);
            }
            return patchesCopy;
        }

        /**
         * Merge a set of patches onto the text.  Return a patched text, as well
         * as an array of true/false values indicating which patches were applied.
         * @param patches Array of Patch objects
         * @param text Old text.
         * @return Two element Object array, containing the new text and an array of
         *      bool values.
         */
        public Object[] patch_apply(List<Patch> patches, string text)
        {
            if (patches.Count == 0)
            {
                return new Object[] { text, new bool[0] };
            }

            // Deep copy the patches so that no changes are made to originals.
            patches = patch_deepCopy(patches);

            string nullPadding = patch_addPadding(patches);
            text = nullPadding + text + nullPadding;
            patch_splitMax(patches);

            int x = 0;
            // delta keeps track of the offset between the expected and actual
            // location of the previous patch.  If there are patches expected at
            // positions 10 and 20, but the first patch was found at 12, delta is 2
            // and the second patch has an effective expected position of 22.
            int delta = 0;
            bool[] results = new bool[patches.Count];
            foreach (Patch aPatch in patches)
            {
                int expectedLoc = aPatch.start2 + delta;
                string text1 = diff_text1(aPatch.diffs);
                int startLoc;
                int endLoc = -1;
                if (text1.Length > _matchMaxBits)
                {
                    // patch_splitMax will only provide an oversized pattern
                    // in the case of a monster delete.
                    startLoc = match_main(text,
                        text1.Substring(0, _matchMaxBits), expectedLoc);
                    if (startLoc != -1)
                    {
                        endLoc = match_main(text,
                            text1.Substring(text1.Length - _matchMaxBits),
                            expectedLoc + text1.Length - _matchMaxBits);
                        if (endLoc == -1 || startLoc >= endLoc)
                        {
                            // Can't find valid trailing context.  Drop this patch.
                            startLoc = -1;
                        }
                    }
                }
                else
                {
                    startLoc = match_main(text, text1, expectedLoc);
                }
                if (startLoc == -1)
                {
                    // No match found.  :(
                    results[x] = false;
                    // Subtract the delta for this failed patch from subsequent patches.
                    delta -= aPatch.length2 - aPatch.length1;
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
                            Math.Min(endLoc + _matchMaxBits, text.Length));
                    }
                    if (text1 == text2)
                    {
                        // Perfect match, just shove the Replacement text in.
                        text = text.Substring(0, startLoc) + diff_text2(aPatch.diffs)
                               + text.Substring(startLoc + text1.Length);
                    }
                    else
                    {
                        // Imperfect match.  Run a diff to get a framework of equivalent
                        // indices.
                        List<Diff> diffs = diff_main(text1, text2, false);
                        if (text1.Length > _matchMaxBits
                            && diff_levenshtein(diffs) / (float)text1.Length
                            > PatchDeleteThreshold)
                        {
                            // The end points match, but the content is unacceptably bad.
                            results[x] = false;
                        }
                        else
                        {
                            diff_cleanupSemanticLossless(diffs);
                            int index1 = 0;
                            foreach (Diff aDiff in aPatch.diffs)
                            {
                                if (aDiff.operation != Operation.EQUAL)
                                {
                                    int index2 = diff_xIndex(diffs, index1);
                                    if (aDiff.operation == Operation.INSERT)
                                    {
                                        // Insertion
                                        text = text.Insert(startLoc + index2, aDiff.text);
                                    }
                                    else if (aDiff.operation == Operation.DELETE)
                                    {
                                        // Deletion
                                        text = text.Remove(startLoc + index2, diff_xIndex(diffs,
                                            index1 + aDiff.text.Length) - index2);
                                    }
                                }
                                if (aDiff.operation != Operation.DELETE)
                                {
                                    index1 += aDiff.text.Length;
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
            return new Object[] { text, results };
        }

        /**
         * Add some padding on text start and end so that edges can match something.
         * Intended to be called only from within patch_apply.
         * @param patches Array of Patch objects.
         * @return The padding string added to each side.
         */
        public string patch_addPadding(List<Patch> patches)
        {
            short paddingLength = PatchMargin;
            StringBuilder nullPaddingSb = new StringBuilder();
            for (short x = 1; x <= paddingLength; x++)
            {
                nullPaddingSb.Append((char)x);
            }
            var nullPadding = nullPaddingSb.ToString();

            // Bump all the patches forward.
            foreach (Patch aPatch in patches)
            {
                aPatch.start1 += paddingLength;
                aPatch.start2 += paddingLength;
            }

            // Add some padding on start of first diff.
            Patch patch = patches.First();
            List<Diff> diffs = patch.diffs;
            if (diffs.Count == 0 || diffs[0].operation != Operation.EQUAL)
            {
                // Add nullPadding equality.
                diffs.Insert(0, Diff.EQUAL(nullPadding));
                patch.start1 -= paddingLength;  // Should be 0.
                patch.start2 -= paddingLength;  // Should be 0.
                patch.length1 += paddingLength;
                patch.length2 += paddingLength;
            }
            else if (paddingLength > diffs[0].text.Length)
            {
                // Grow first equality.
                Diff firstDiff = diffs[0];
                int extraLength = nullPadding.Length - firstDiff.text.Length;
                diffs[0] = firstDiff.Replace(nullPadding.Substring(firstDiff.text.Length) + firstDiff.text);
                patch.start1 -= extraLength;
                patch.start2 -= extraLength;
                patch.length1 += extraLength;
                patch.length2 += extraLength;
            }

            // Add some padding on end of last diff.
            patch = patches.Last();
            diffs = patch.diffs;
            if (diffs.Count == 0 || diffs.Last().operation != Operation.EQUAL)
            {
                // Add nullPadding equality.
                diffs.Add(Diff.EQUAL(nullPadding));
                patch.length1 += paddingLength;
                patch.length2 += paddingLength;
            }
            else if (paddingLength > diffs[diffs.Count - 1].text.Length)
            {
                // Grow last equality.
                Diff lastDiff = diffs[diffs.Count - 1];
                int extraLength = nullPadding.Length - lastDiff.text.Length;
                var text = lastDiff.text + nullPadding.Substring(0, extraLength);
                diffs[diffs.Count - 1] = lastDiff.Replace(text);
                patch.length1 += extraLength;
                patch.length2 += extraLength;
            }

            return nullPadding;
        }

        /**
         * Look through the patches and break up any which are longer than the
         * maximum limit of the match algorithm.
         * Intended to be called only from within patch_apply.
         * @param patches List of Patch objects.
         */
        public void patch_splitMax(List<Patch> patches)
        {
            short patchSize = _matchMaxBits;
            for (int x = 0; x < patches.Count; x++)
            {
                if (patches[x].length1 <= patchSize)
                {
                    continue;
                }
                Patch bigpatch = patches[x];
                // Remove the big old patch.
                patches.Splice(x--, 1);
                int start1 = bigpatch.start1;
                int start2 = bigpatch.start2;
                string precontext = string.Empty;
                var diffs = bigpatch.diffs;
                while (diffs.Count != 0)
                {
                    // Create one of several smaller patches.
                    Patch patch = new Patch();
                    bool empty = true;
                    patch.start1 = start1 - precontext.Length;
                    patch.start2 = start2 - precontext.Length;
                    if (precontext.Length != 0)
                    {
                        patch.length1 = patch.length2 = precontext.Length;
                        patch.diffs.Add(Diff.EQUAL(precontext));
                    }
                    while (diffs.Count != 0
                           && patch.length1 < patchSize - PatchMargin)
                    {
                        Operation diffType = diffs[0].operation;
                        string diffText = diffs[0].text;
                        if (diffType == Operation.INSERT)
                        {
                            // Insertions are harmless.
                            patch.length2 += diffText.Length;
                            start2 += diffText.Length;
                            patch.diffs.Add(diffs.First());
                            diffs.RemoveAt(0);
                            empty = false;
                        }
                        else if (diffType == Operation.DELETE && patch.diffs.Count == 1
                                 && patch.diffs.First().operation == Operation.EQUAL
                                 && diffText.Length > 2 * patchSize)
                        {
                            // This is a large deletion.  Let it pass in one chunk.
                            patch.length1 += diffText.Length;
                            start1 += diffText.Length;
                            empty = false;
                            patch.diffs.Add(Diff.Create(diffType, diffText));
                            diffs.RemoveAt(0);
                        }
                        else
                        {
                            // Deletion or equality.  Only take as much as we can stomach.
                            diffText = diffText.Substring(0, Math.Min(diffText.Length,
                                patchSize - patch.length1 - PatchMargin));
                            patch.length1 += diffText.Length;
                            start1 += diffText.Length;
                            if (diffType == Operation.EQUAL)
                            {
                                patch.length2 += diffText.Length;
                                start2 += diffText.Length;
                            }
                            else
                            {
                                empty = false;
                            }
                            patch.diffs.Add(Diff.Create(diffType, diffText));
                            if (diffText == diffs[0].text)
                            {
                                diffs.RemoveAt(0);
                            }
                            else
                            {
                                diffs[0] = diffs[0].Replace(diffs[0].text.Substring(diffText.Length));
                            }
                        }
                    }
                    // Compute the head context for the next patch.
                    precontext = diff_text2(patch.diffs);
                    precontext = precontext.Substring(Math.Max(0,
                        precontext.Length - PatchMargin));

                    string postcontext = null;
                    // Append the end context for this patch.
                    if (diff_text1(diffs).Length > PatchMargin)
                    {
                        postcontext = diff_text1(diffs)
                            .Substring(0, PatchMargin);
                    }
                    else
                    {
                        postcontext = diff_text1(diffs);
                    }

                    if (postcontext.Length != 0)
                    {
                        patch.length1 += postcontext.Length;
                        patch.length2 += postcontext.Length;
                        if (patch.diffs.Count != 0
                            && patch.diffs[patch.diffs.Count - 1].operation == Operation.EQUAL)
                        {
                            patch.diffs[patch.diffs.Count - 1] = patch.diffs[patch.diffs.Count - 1].Replace(patch.diffs[patch.diffs.Count - 1].text + postcontext);
                        }
                        else
                        {
                            patch.diffs.Add(Diff.EQUAL(postcontext));
                        }
                    }
                    if (!empty)
                    {
                        patches.Splice(++x, 0, patch);
                    }
                }
            }
        }

        /**
         * Take a list of patches and return a textual representation.
         * @param patches List of Patch objects.
         * @return Text representation of patches.
         */
        public string patch_toText(List<Patch> patches)
        {
            StringBuilder text = new StringBuilder();
            foreach (Patch aPatch in patches)
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
        public List<Patch> patch_fromText(string textline)
        {
            List<Patch> patches = new List<Patch>();
            if (textline.Length == 0)
            {
                return patches;
            }
            string[] text = textline.Split('\n');
            int textPointer = 0;
            Patch patch;
            Regex patchHeader
                = new Regex("^@@ -(\\d+),?(\\d*) \\+(\\d+),?(\\d*) @@$");
            Match m;
            char sign;
            string line;
            while (textPointer < text.Length)
            {
                m = patchHeader.Match(text[textPointer]);
                if (!m.Success)
                {
                    throw new ArgumentException("Invalid patch string: "
                                                + text[textPointer]);
                }
                patch = new Patch();
                patches.Add(patch);
                patch.start1 = Convert.ToInt32(m.Groups[1].Value);
                if (m.Groups[2].Length == 0)
                {
                    patch.start1--;
                    patch.length1 = 1;
                }
                else if (m.Groups[2].Value == "0")
                {
                    patch.length1 = 0;
                }
                else
                {
                    patch.start1--;
                    patch.length1 = Convert.ToInt32(m.Groups[2].Value);
                }

                patch.start2 = Convert.ToInt32(m.Groups[3].Value);
                if (m.Groups[4].Length == 0)
                {
                    patch.start2--;
                    patch.length2 = 1;
                }
                else if (m.Groups[4].Value == "0")
                {
                    patch.length2 = 0;
                }
                else
                {
                    patch.start2--;
                    patch.length2 = Convert.ToInt32(m.Groups[4].Value);
                }
                textPointer++;

                while (textPointer < text.Length)
                {
                    try
                    {
                        sign = text[textPointer][0];
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // Blank line?  Whatever.
                        textPointer++;
                        continue;
                    }
                    line = text[textPointer].Substring(1);
                    line = line.Replace("+", "%2b");
                    line = HttpUtility.UrlDecode(line, new UTF8Encoding(false, true));
                    if (sign == '-')
                    {
                        // Deletion.
                        patch.diffs.Add(Diff.DELETE(line));
                    }
                    else if (sign == '+')
                    {
                        // Insertion.
                        patch.diffs.Add(Diff.INSERT(line));
                    }
                    else if (sign == ' ')
                    {
                        // Minor equality.
                        patch.diffs.Add(Diff.EQUAL(line));
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
        public static string UnescapeForEncodeUriCompatability(string str)
        {
            return str.Replace("%21", "!").Replace("%7e", "~")
                .Replace("%27", "'").Replace("%28", "(").Replace("%29", ")")
                .Replace("%3b", ";").Replace("%2f", "/").Replace("%3f", "?")
                .Replace("%3a", ":").Replace("%40", "@").Replace("%26", "&")
                .Replace("%3d", "=").Replace("%2b", "+").Replace("%24", "$")
                .Replace("%2c", ",").Replace("%23", "#");
        }
    }
}