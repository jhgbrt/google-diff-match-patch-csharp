using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace DiffMatchPatch
{
    public class Diff
    {
        public static Diff Create(Operation operation, string text)
        {
            return new Diff(operation, text);
        }

        public static Diff Equal(string text)
        {
            return Create(Operation.Equal, text);
        }

        public static Diff Insert(string text)
        {
            return Create(Operation.Insert, text);
        }
        public static Diff Delete(string text)
        {
            return Create(Operation.Delete, text);
        }

        public readonly Operation Operation;
        // One of: INSERT, DELETE or EQUAL.
        public readonly string Text;
        // The text associated with this diff operation.

        Diff(Operation operation, string text)
        {
            // Construct a diff with the specified operation and text.
            this.Operation = operation;
            this.Text = text;
        }

        /// <summary>
        /// Generate a human-readable version of this Diff.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var prettyText = Text.Replace('\n', '\u00b6');
            return "Diff(" + Operation + ",\"" + prettyText + "\")";
        }

        /// <summary>
        /// Is this Diff equivalent to another Diff?
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(Object obj)
        {
            var p = obj as Diff;
            return p != null && p.Operation == Operation && p.Text == Text;
        }

        public bool Equals(Diff obj)
        {
            return obj != null && obj.Operation == Operation && obj.Text == Text;

        }

        public override int GetHashCode()
        {
            return Text.GetHashCode() ^ Operation.GetHashCode();
        }

        public Diff Replace(string toString)
        {
            return Create(Operation, toString);
        }

        public Diff Copy()
        {
            return Create(Operation, Text);
        }

        /// <summary>
        /// Find the differences between two texts.
        /// </summary>
        /// <param name="text1">Old string to be diffed</param>
        /// <param name="text2">New string to be diffed</param>
        /// <param name="timeoutInSeconds">if specified, certain optimizations may be enabled to meet the time constraint, possibly resulting in a less optimal diff</param>
        /// <param name="checklines">If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
        /// <returns></returns>
        public static List<Diff> Compute(string text1, string text2, float timeoutInSeconds = 0f, bool checklines = true)
        {
            CancellationTokenSource cts;
            if (timeoutInSeconds <= 0)
            {
                cts = new CancellationTokenSource();
            }
            else
            {
                var waitTime = TimeSpan.FromSeconds(timeoutInSeconds);
                cts = new CancellationTokenSource(waitTime);
            }
            return Compute(text1, text2, checklines, cts.Token, optimizeForSpeed: timeoutInSeconds > 0);
        }

       
        /// <summary>
        /// Find the differences between two texts.  Simplifies the problem by
        /// stripping any common prefix or suffix off the texts before diffing.
        /// </summary>
        /// <param name="text1">Old string to be diffed.</param>
        /// <param name="text2">New string to be diffed.</param>
        /// <param name="checklines">Speedup flag.  If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
        /// <param name="token">Cancellation token for cooperative cancellation</param>
        /// <param name="optimizeForSpeed">Should optimizations be enabled?</param>
        /// <returns></returns>
        private static List<Diff> Compute(string text1, string text2, bool checklines, CancellationToken token, bool optimizeForSpeed)
        {
            // Check for null inputs not needed since null can't be passed in C#.

            // Check for equality (speedup).
            List<Diff> diffs;
            if (text1 == text2)
            {
                diffs = new List<Diff>();
                if (text1.Length != 0)
                {
                    diffs.Add(Equal(text1));
                }
                return diffs;
            }

            // Trim off common prefix (speedup).
            var commonlength = TextUtil.CommonPrefix(text1, text2);
            var commonprefix = text1.Substring(0, commonlength);
            text1 = text1.Substring(commonlength);
            text2 = text2.Substring(commonlength);

            // Trim off common suffix (speedup).
            commonlength = TextUtil.CommonSuffix(text1, text2);
            var commonsuffix = text1.Substring(text1.Length - commonlength);
            text1 = text1.Substring(0, text1.Length - commonlength);
            text2 = text2.Substring(0, text2.Length - commonlength);

            // Compute the diff on the middle block.
            diffs = ComputeImpl(text1, text2, checklines, token, optimizeForSpeed);

            // Restore the prefix and suffix.
            if (commonprefix.Length != 0)
            {
                diffs.Insert(0, Equal(commonprefix));
            }
            if (commonsuffix.Length != 0)
            {
                diffs.Add(Equal(commonsuffix));
            }

            diffs.CleanupMerge();
            return diffs;
        }

        /// <summary>
        /// Find the differences between two texts.  Assumes that the texts do not
        /// have any common prefix or suffix.
        /// </summary>
        /// <param name="text1">Old string to be diffed.</param>
        /// <param name="text2">New string to be diffed.</param>
        /// <param name="checklines">Speedup flag.  If false, then don't run a line-level diff first to identify the changed areas. If true, then run a faster slightly less optimal diff.</param>
        /// <param name="token">Cancellation token for cooperative cancellation</param>
        /// <param name="optimizeForSpeed">Should optimizations be enabled?</param>
        /// <returns></returns>
        private static List<Diff> ComputeImpl(
            string text1, 
            string text2,
            bool checklines, CancellationToken token, bool optimizeForSpeed)
        {
            var diffs = new List<Diff>();

            if (text1.Length == 0)
            {
                // Just add some text (speedup).
                diffs.Add(Insert(text2));
                return diffs;
            }

            if (text2.Length == 0)
            {
                // Just delete some text (speedup).
                diffs.Add(Delete(text1));
                return diffs;
            }

            var longtext = text1.Length > text2.Length ? text1 : text2;
            var shorttext = text1.Length > text2.Length ? text2 : text1;
            var i = longtext.IndexOf(shorttext, StringComparison.Ordinal);
            if (i != -1)
            {
                // Shorter text is inside the longer text (speedup).
                var op = text1.Length > text2.Length ? Operation.Delete : Operation.Insert;
                diffs.Add(Create(op, longtext.Substring(0, i)));
                diffs.Add(Equal(shorttext));
                diffs.Add(Create(op, longtext.Substring(i + shorttext.Length)));
                return diffs;
            }

            if (shorttext.Length == 1)
            {
                // Single character string.
                // After the previous speedup, the character can't be an equality.
                diffs.Add(Delete(text1));
                diffs.Add(Insert(text2));
                return diffs;
            }

            // Don't risk returning a non-optimal diff if we have unlimited time.
            if (optimizeForSpeed)
            {
                // Check to see if the problem can be split in two.
                var result = TextUtil.HalfMatch(text1, text2);
                if (result != null)
                {
                    Trace.WriteLine("half match");
                    // A half-match was found, sort out the return data.
                    // Send both pairs off for separate processing.
                    var diffsA = Compute(result.Prefix1, result.Prefix1, checklines, token, false);
                    var diffsB = Compute(result.Suffix1, result.Suffix2, checklines, token, false);

                    // Merge the results.
                    diffs = diffsA;
                    diffs.Add(Equal(result.CommonMiddle));
                    diffs.AddRange(diffsB);
                    return diffs;
                }
            }
            if (checklines && text1.Length > 100 && text2.Length > 100)
            {
                return LineDiff(text1, text2, token, optimizeForSpeed);
            }

            return MyersDiffBisect(text1, text2, token, optimizeForSpeed);
        }

        /// <summary>
        /// Do a quick line-level diff on both strings, then rediff the parts for
        /// greater accuracy. This speedup can produce non-minimal Diffs.
        /// </summary>
        /// <param name="text1"></param>
        /// <param name="text2"></param>
        /// <param name="token"></param>
        /// <param name="optimizeForSpeed"></param>
        /// <returns></returns>
        private static List<Diff> LineDiff(string text1, string text2, CancellationToken token, bool optimizeForSpeed)
        {
            // Scan the text on a line-by-line basis first.
            var b = TextUtil.LinesToChars(text1, text2);
            text1 = b.Item1;
            text2 = b.Item2;
            var linearray = b.Item3;

            var diffs = Compute(text1, text2, false, token, optimizeForSpeed);

            // Convert the diff back to original text.
            diffs = diffs.CharsToLines(linearray).ToList();
            // Eliminate freak matches (e.g. blank lines)
            diffs.CleanupSemantic();

            // Rediff any replacement blocks, this time character-by-character.
            // Add a dummy entry at the end.
            diffs.Add(Equal(string.Empty));
            var pointer = 0;
            var countDelete = 0;
            var countInsert = 0;
            var textDelete = string.Empty;
            var textInsert = string.Empty;
            while (pointer < diffs.Count)
            {
                switch (diffs[pointer].Operation)
                {
                    case Operation.Insert:
                        countInsert++;
                        textInsert += diffs[pointer].Text;
                        break;
                    case Operation.Delete:
                        countDelete++;
                        textDelete += diffs[pointer].Text;
                        break;
                    case Operation.Equal:
                        // Upon reaching an equality, check for prior redundancies.
                        if (countDelete >= 1 && countInsert >= 1)
                        {
                            // Delete the offending records and add the merged ones.
                            var a = Compute(textDelete, textInsert, false, token, optimizeForSpeed);
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

        /// <summary>
        /// Find the 'middle snake' of a diff, split the problem in two
        /// and return the recursively constructed diff.
        /// See Myers 1986 paper: An O(ND) Difference Algorithm and Its Variations.
        /// </summary>
        /// <param name="text1"></param>
        /// <param name="text2"></param>
        /// <param name="token"></param>
        /// <param name="optimizeForSpeed"></param>
        /// <returns></returns>
        internal static List<Diff> MyersDiffBisect(string text1, string text2, CancellationToken token, bool optimizeForSpeed)
        {
            // Cache the text lengths to prevent multiple calls.
            var text1Length = text1.Length;
            var text2Length = text2.Length;
            var maxD = (text1Length + text2Length + 1) / 2;
            var vOffset = maxD;
            var vLength = 2 * maxD;
            var v1 = new int[vLength];
            var v2 = new int[vLength];
            for (var x = 0; x < vLength; x++)
            {
                v1[x] = -1;
            }
            for (var x = 0; x < vLength; x++)
            {
                v2[x] = -1;
            }
            v1[vOffset + 1] = 0;
            v2[vOffset + 1] = 0;
            var delta = text1Length - text2Length;
            // If the total number of characters is odd, then the front path will
            // collide with the reverse path.
            var front = delta % 2 != 0;
            // Offsets for start and end of k loop.
            // Prevents mapping of space beyond the grid.
            var k1Start = 0;
            var k1End = 0;
            var k2Start = 0;
            var k2End = 0;
            for (var d = 0; d < maxD; d++)
            {
                // Bail out if cancelled.
                if (token.IsCancellationRequested)
                {
                    break;
                }

                // Walk the front path one step.
                for (var k1 = -d + k1Start; k1 <= d - k1End; k1 += 2)
                {
                    var k1Offset = vOffset + k1;
                    int x1;
                    if (k1 == -d || k1 != d && v1[k1Offset - 1] < v1[k1Offset + 1])
                    {
                        x1 = v1[k1Offset + 1];
                    }
                    else
                    {
                        x1 = v1[k1Offset - 1] + 1;
                    }
                    var y1 = x1 - k1;
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
                        var k2Offset = vOffset + delta - k1;
                        if (k2Offset >= 0 && k2Offset < vLength && v2[k2Offset] != -1)
                        {
                            // Mirror x2 onto top-left coordinate system.
                            var x2 = text1Length - v2[k2Offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return BisectSplit(text1, text2, x1, y1, token, optimizeForSpeed);
                            }
                        }
                    }
                }

                // Walk the reverse path one step.
                for (var k2 = -d + k2Start; k2 <= d - k2End; k2 += 2)
                {
                    var k2Offset = vOffset + k2;
                    int x2;
                    if (k2 == -d || k2 != d && v2[k2Offset - 1] < v2[k2Offset + 1])
                    {
                        x2 = v2[k2Offset + 1];
                    }
                    else
                    {
                        x2 = v2[k2Offset - 1] + 1;
                    }
                    var y2 = x2 - k2;
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
                        var k1Offset = vOffset + delta - k2;
                        if (k1Offset >= 0 && k1Offset < vLength && v1[k1Offset] != -1)
                        {
                            var x1 = v1[k1Offset];
                            var y1 = vOffset + x1 - k1Offset;
                            // Mirror x2 onto top-left coordinate system.
                            x2 = text1Length - v2[k2Offset];
                            if (x1 >= x2)
                            {
                                // Overlap detected.
                                return BisectSplit(text1, text2, x1, y1, token, optimizeForSpeed);
                            }
                        }
                    }
                }
            }
            // Diff took too long and hit the deadline or
            // number of Diffs equals number of characters, no commonality at all.
            var diffs = new List<Diff> { Delete(text1), Insert(text2) };
            return diffs;
        }

        /// <summary>
        /// Given the location of the 'middle snake', split the diff in two parts
        /// and recurse.
        /// </summary>
        /// <param name="text1"></param>
        /// <param name="text2"></param>
        /// <param name="x">Index of split point in text1.</param>
        /// <param name="y">Index of split point in text2.</param>
        /// <param name="token"></param>
        /// <param name="optimizeForSpeed"></param>
        /// <returns></returns>
        private static List<Diff> BisectSplit(string text1, string text2, int x, int y, CancellationToken token, bool optimizeForSpeed)
        {
            var text1A = text1.Substring(0, x);
            var text2A = text2.Substring(0, y);
            var text1B = text1.Substring(x);
            var text2B = text2.Substring(y);

            // Compute both Diffs serially.
            var diffs = Compute(text1A, text2A, false, token, optimizeForSpeed);
            var diffsb = Compute(text1B, text2B, false, token, optimizeForSpeed);

            diffs.AddRange(diffsb);
            return diffs;
        }

    }
}