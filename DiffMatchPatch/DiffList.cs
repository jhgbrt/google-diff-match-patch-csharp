using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static DiffMatchPatch.Operation;

namespace DiffMatchPatch
{
    public static class DiffList
    {
        /// <summary>
        /// Compute and return the source text (all equalities and deletions).
        /// </summary>
        /// <param name="diffs"></param>
        /// <returns></returns>
        public static string Text1(this IEnumerable<Diff> diffs)
            => diffs
            .Where(d => d.Operation != Insert)
            .Aggregate(new StringBuilder(), (sb, diff) => sb.Append(diff.Text))
            .ToString();

        /// <summary>
        /// Compute and return the destination text (all equalities and insertions).
        /// </summary>
        /// <param name="diffs"></param>
        /// <returns></returns>
        public static string Text2(this IEnumerable<Diff> diffs)
            => diffs
            .Where(d => d.Operation != Delete)
            .Aggregate(new StringBuilder(), (sb, diff) => sb.Append(diff.Text))
            .ToString();

        record LevenshteinState(int Insertions, int Deletions, int Levenshtein)
        {
            public LevenshteinState Consolidate() => new LevenshteinState(0, 0, Levenshtein + Math.Max(Insertions, Deletions));
        }

        /// <summary>
        /// Compute the Levenshtein distance; the number of inserted, deleted or substituted characters.
        /// </summary>
        /// <param name="diffs"></param>
        /// <returns></returns>
        public static int Levenshtein(this IEnumerable<Diff> diffs)
        {
            var state = new LevenshteinState(0, 0, 0);
            foreach (var aDiff in diffs)
            {
                state = aDiff.Operation switch
                {
                    Insert => state with { Insertions = state.Insertions + aDiff.Text.Length },
                    Delete => state with { Deletions = state.Deletions + aDiff.Text.Length },
                    Equal => state.Consolidate(),
                    _ => throw new IndexOutOfRangeException()
                };
            }
            return state.Consolidate().Levenshtein;
        }
        private static StringBuilder AppendHtml(this StringBuilder sb, string tag, string backgroundColor, string content)
            => sb
            .Append(string.IsNullOrEmpty(backgroundColor) ? $"<{tag}>" : $"<{tag} style=\"background:{backgroundColor};\">")
            .Append(content)
            .Append($"</{tag}>");

        private static StringBuilder AppendHtml(this StringBuilder sb, Operation operation, string text) => operation switch
        {
            Insert => sb.AppendHtml("ins", "#e6ffe6", text),
            Delete => sb.AppendHtml("del", "#ffe6e6", text),
            Equal => sb.AppendHtml("span", "", text),
            _ => throw new ArgumentException()
        };

        /// <summary>
        /// Convert a Diff list into a pretty HTML report.
        /// </summary>
        /// <param name="diffs"></param>
        /// <returns></returns>
        public static string PrettyHtml(this IEnumerable<Diff> diffs) => diffs
            .Aggregate(new StringBuilder(), (sb, diff) => sb.AppendHtml(diff.Operation, diff.Text.HtmlEncodeLight()))
            .ToString();

        private static string HtmlEncodeLight(this string s)
        {
            var text = new StringBuilder(s)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\n", "&para;<br>")
                .ToString();
            return text;
        }

        static char ToDelta(this Operation o) => o switch
        {
            Delete => '-',
            Insert => '+',
            Equal => '=',
            _ => throw new ArgumentException($"Unknown Operation: {o}")
        };

        static Operation FromDelta(char c) => c switch
        {
            '-' => Delete,
            '+' => Insert,
            '=' => Equal,
            _ => throw new ArgumentException($"Invalid Delta Token: {c}")
        };

        /// <summary>
        /// Crush the diff into an encoded string which describes the operations
        /// required to transform text1 into text2.
        /// E.g. =3\t-2\t+ing  -> Keep 3 chars, delete 2 chars, insert 'ing'.
        /// Operations are tab-separated.  Inserted text is escaped using %xx
        /// notation.
        /// </summary>
        /// <param name="diffs"></param>
        /// <returns></returns>
        public static string ToDelta(this IEnumerable<Diff> diffs)
        {
            var s =
                from aDiff in diffs
                let sign = aDiff.Operation.ToDelta()
                let textToAppend = aDiff.Operation == Insert
                    ? aDiff.Text.UrlEncoded()
                    : aDiff.Text.Length.ToString()
                select string.Concat(sign, textToAppend);

            var delta = string.Join("\t", s);
            return delta;
        }

        /// <summary>
        /// Given the original text1, and an encoded string which describes the
        /// operations required to transform text1 into text2, compute the full diff.
        /// </summary>
        /// <param name="text1">Source string for the diff.</param>
        /// <param name="delta">Delta text.</param>
        /// <returns></returns>
        public static IEnumerable<Diff> FromDelta(string text1, string delta)
        {
            var pointer = 0;  // Cursor in text1

            foreach (var token in delta.SplitBy('\t'))
            {
                if (token.Length == 0)
                {
                    // Blank tokens are ok (from a trailing \t).
                    continue;
                }
                // Each token begins with a one character parameter which specifies the
                // operation of this token (delete, insert, equality).
                var param = token[1..];
                var operation = FromDelta(token[0]);
                int n = 0;
                if (operation != Insert)
                {
                    if (!int.TryParse(param, out n))
                    {
                        throw new ArgumentException($"Invalid number in Diff.FromDelta: {param}");
                    }
                    if (pointer < 0 || n < 0 || pointer > text1.Length - n)
                    {
                        throw new ArgumentException($"Delta length ({pointer}) larger than source text length ({text1.Length}).");
                    }
                }
                string text;
                (text, pointer) = operation switch
                {
                    Insert => (param.Replace("+", "%2b").UrlDecoded(), pointer),
                    Equal => (text1.Substring(pointer, n), pointer + n),
                    Delete => (text1.Substring(pointer, n), pointer + n),
                    _ => throw new ArgumentException($"Unknown Operation: {operation}")
                };
                yield return Diff.Create(operation, text);
            }
            if (pointer != text1.Length)
            {
                throw new ArgumentException($"Delta length ({pointer}) smaller than source text length ({text1.Length}).");
            }
        }

        /// <summary>
        /// Reorder and merge like edit sections.  Merge equalities.
        /// Any edit section can move as long as it doesn't cross an equality.
        /// </summary>
        /// <param name="diffs">list of Diffs</param>
        internal static IEnumerable<Diff> CleanupMerge(this List<Diff> diffs)
        {
            // Add a dummy entry at the beginning & end.
            diffs.Insert(0, Diff.Empty);
            diffs.Add(Diff.Empty);
            var nofdiffs = 0;
            var sbDelete = new StringBuilder();
            var sbInsert = new StringBuilder();
            var pointer = 1;
            var lastEquality = 0;
            while (pointer < diffs.Count)
            {
                var diff = diffs[pointer];

                (sbInsert, sbDelete) = diff.Operation switch
                {
                    Insert => (sbInsert.Append(diff.Text), sbDelete),
                    Delete => (sbInsert, sbDelete.Append(diff.Text)),
                    _ => (sbInsert, sbDelete)
                };

                switch (diff.Operation)
                {
                    case Insert:
                    case Delete:
                        nofdiffs++;
                        pointer++;
                        break;
                    case Equal:
                        // Upon reaching an equality, check for prior redundancies.
                        if (sbInsert.Length > 0 || sbDelete.Length > 0)
                        {
                            // first equality after number of inserts/deletes
                            // Factor out any common prefixies.
                            var prefixLength = TextUtil.CommonPrefix(sbInsert, sbDelete);
                            if (prefixLength > 0)
                            {
                                var commonprefix = sbInsert.ToString(0, prefixLength);
                                sbInsert.Remove(0, prefixLength);
                                sbDelete.Remove(0, prefixLength);
                                diffs[lastEquality] = diffs[lastEquality].Append(commonprefix);
                            }


                            // Factor out any common suffixies.
                            var suffixLength = TextUtil.CommonSuffix(sbInsert, sbDelete);
                            if (suffixLength > 0)
                            {
                                var commonsuffix = sbInsert.ToString(sbInsert.Length - suffixLength, suffixLength);
                                sbInsert.Remove(sbInsert.Length - suffixLength, suffixLength);
                                sbDelete.Remove(sbDelete.Length - suffixLength, suffixLength);
                                diffs[pointer] = diffs[pointer].Prepend(commonsuffix);
                            }

                            // Delete the offending records and add the merged ones.
                            IEnumerable<Diff> Replacements()
                            {
                                if (sbDelete.Length > 0) yield return Diff.Delete(sbDelete.ToString());
                                if (sbInsert.Length > 0) yield return Diff.Insert(sbInsert.ToString());
                            }

                            var replacements = Replacements().ToList();
                            diffs.Splice(pointer - nofdiffs, nofdiffs, replacements);

                            pointer = pointer - nofdiffs + replacements.Count + 1;
                        }
                        else if (pointer > 0 && lastEquality == pointer - 1)
                        {
                            // Merge this equality with the previous one.
                            diffs[lastEquality] = diffs[lastEquality].Append(diffs[pointer].Text);
                            diffs.RemoveAt(pointer);
                        }
                        else
                        {
                            pointer++;
                        }
                        nofdiffs = 0;
                        sbDelete.Clear();
                        sbInsert.Clear();
                        lastEquality = pointer - 1;
                        break;
                }
            }
            if (diffs[0].IsEmpty && diffs.Count > 1)
            {
                diffs.RemoveAt(0);
            }
            if (diffs[^1].IsEmpty)
            {
                diffs.RemoveAt(diffs.Count - 1);  // Remove the dummy entry at the end.
            }

            // Second pass: look for single edits surrounded on both sides by
            // equalities which can be shifted sideways to eliminate an equality.
            // e.g: A<ins>BA</ins>C -> <ins>AB</ins>AC
            var changes = false;
            // Intentionally ignore the first and last element (don't need checking).
            for (var i = 1; i < diffs.Count - 1; i++)
            {
                var previous = diffs[i - 1];
                var current = diffs[i];
                var next = diffs[i + 1];
                if (previous.Operation == Equal && next.Operation == Equal)
                {
                    // This is a single edit surrounded by equalities.
                    if (current.Text.EndsWith(previous.Text, StringComparison.Ordinal))
                    {
                        // Shift the edit over the previous equality.
                        var text = previous.Text + current.Text.Substring(0, current.Text.Length - previous.Text.Length);
                        diffs[i] = current.Replace(text);
                        diffs[i + 1] = next.Replace(previous.Text + next.Text);
                        diffs.Splice(i - 1, 1);
                        changes = true;
                    }
                    else if (current.Text.StartsWith(next.Text, StringComparison.Ordinal))
                    {
                        // Shift the edit over the next equality.
                        diffs[i - 1] = previous.Replace(previous.Text + next.Text);
                        diffs[i] = current.Replace(current.Text[next.Text.Length..] + next.Text);
                        diffs.Splice(i + 1, 1);
                        changes = true;
                    }
                }
            }
            // If shifts were made, the diff needs reordering and another shift sweep.
            if (changes)
            {
                diffs.CleanupMerge();
            }
            return diffs;
        }

        record EditBetweenEqualities(string Equality1, string Edit, string Equality2)
        {
            public int Score => DiffCleanupSemanticScore(Equality1, Edit) + DiffCleanupSemanticScore(Edit, Equality2);

            // Shift the edit as far left as possible.
            public EditBetweenEqualities ShiftLeft()
            {
                var commonOffset = TextUtil.CommonSuffix(Equality1, Edit);

                if (commonOffset > 0)
                {
                    var commonString = Edit[^commonOffset..];
                    return this with { Equality1 = Equality1.Substring(0, Equality1.Length - commonOffset), Edit = commonString + Edit.Substring(0, Edit.Length - commonOffset), Equality2 = commonString + Equality2 };
                }
                else
                {
                    return this;
                }
            }

            // Shift one right
            EditBetweenEqualities ShiftRight() => this with { Equality1 = Equality1 + Edit[0], Edit = Edit[1..] + Equality2[0], Equality2 = Equality2[1..] };

            public IEnumerable<EditBetweenEqualities> TraverseRight()
            {
                var item = this;
                while (item.Edit.Length != 0 && item.Equality2.Length != 0 && item.Edit[0] == item.Equality2[0])
                {
                    yield return item = item.ShiftRight();
                }
            }

            public IEnumerable<Diff> ToDiffs(Operation edit)
            {
                yield return Diff.Equal(Equality1);
                yield return Diff.Create(edit, Edit);
                yield return Diff.Equal(Equality2);
            }
        }

        /// <summary>
        /// Look for single edits surrounded on both sides by equalities
        /// which can be shifted sideways to align the edit to a word boundary.
        /// e.g: The c<ins>at c</ins>ame. -> The <ins>cat </ins>came.
        /// </summary>
        /// <param name="diffs"></param>
        internal static IEnumerable<Diff> CleanupSemanticLossless(this IEnumerable<Diff> diffs)
        {
            var enumerator = diffs.GetEnumerator();

            if (!enumerator.MoveNext()) yield break;

            var previous = enumerator.Current;
            
            if (!enumerator.MoveNext())
            {
                yield return previous;
                yield break;
            }
            
            var current = enumerator.Current;
            
            while (true)
            {
                if (!enumerator.MoveNext())
                {
                    yield return previous;
                    yield return current;
                    yield break;
                }

                var next = enumerator.Current;

                if (previous.Operation == Equal && next.Operation == Equal)
                {
                    // This is a single edit surrounded by equalities.
                    var item = new EditBetweenEqualities(previous.Text, current.Text, next.Text).ShiftLeft();

                    // Second, step character by character right, looking for the best fit.
                    var best = item.TraverseRight().Aggregate(item, (best, x) => best.Score > x.Score ? best : x);

                    if (previous.Text != best.Equality1)
                    {
                        // We have an improvement; yield the improvement instead of the original diffs
                        foreach (var d in best.ToDiffs(current.Operation).Where(d =>!d.IsEmpty)) 
                            yield return d;
                        
                        if (!enumerator.MoveNext()) 
                            yield break;

                        previous = current;
                        current = next;
                        next = enumerator.Current;
                    }
                    else
                    {
                        yield return previous;
                    }
                }
                else
                {
                    yield return previous;
                }

                previous = current;
                current = next;
            }
        }

        /// <summary>
        /// Given two strings, compute a score representing whether the internal
        /// boundary falls on logical boundaries.
        /// Scores range from 6 (best) to 0 (worst).
        ///  </summary>
        /// <param name="one"></param>
        /// <param name="two"></param>
        /// <returns>score</returns>
        private static int DiffCleanupSemanticScore(string one, string two)
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

            Index i = ^1;
            var char1 = one[^1];
            var char2 = two[0];
            var nonAlphaNumeric1 = !char.IsLetterOrDigit(char1);
            var nonAlphaNumeric2 = !char.IsLetterOrDigit(char2);
            var whitespace1 = char.IsWhiteSpace(char1);
            var whitespace2 = char.IsWhiteSpace(char2);
            var lineBreak1 = char1 == '\n' || char1 == '\r';
            var lineBreak2 = char1 == '\n' || char1 == '\r';
            var blankLine1 = lineBreak1 && BlankLineEnd.IsMatch(one);
            var blankLine2 = lineBreak2 && BlankLineStart.IsMatch(two);

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
        private static readonly Regex BlankLineEnd = new Regex("\\n\\r?\\n\\Z", RegexOptions.Compiled);
        private static readonly Regex BlankLineStart = new Regex("\\A\\r?\\n\\r?\\n", RegexOptions.Compiled);

        /// <summary>
        /// Reduce the number of edits by eliminating operationally trivial equalities.
        /// </summary>
        /// <param name="diffs"></param>
        /// <param name="diffEditCost"></param>
        internal static List<Diff> CleanupEfficiency(this List<Diff> diffs, short diffEditCost = 4)
        {
            var changes = false;
            // Stack of indices where equalities are found.
            var equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            var lastEquality = string.Empty;
            // Is there an insertion operation before the last equality.
            var insertionBeforeLastEquality = false;
            // Is there a deletion operation before the last equality.
            var deletionBeforeLastEquality = false;
            // Is there an insertion operation after the last equality.
            var insertionAfterLastEquality = false;
            // Is there a deletion operation after the last equality.
            var deletionAfterLastEquality = false;

            for (var i = 0; i < diffs.Count; i++)
            {
                if (diffs[i].Operation == Equal)
                {  // Equality found.
                    if (diffs[i].Text.Length < diffEditCost && (insertionAfterLastEquality || deletionAfterLastEquality))
                    {
                        // Candidate found.
                        equalities.Push(i);
                        (insertionBeforeLastEquality, deletionBeforeLastEquality) = (insertionAfterLastEquality, deletionAfterLastEquality);
                        lastEquality = diffs[i].Text;
                    }
                    else
                    {
                        // Not a candidate, and can never become one.
                        equalities.Clear();
                        lastEquality = string.Empty;
                    }
                    insertionAfterLastEquality = deletionAfterLastEquality = false;
                }
                else
                {  // An insertion or deletion.
                    if (diffs[i].Operation == Delete)
                    {
                        deletionAfterLastEquality = true;
                    }
                    else
                    {
                        insertionAfterLastEquality = true;
                    }
                    /*
                     * Five types to be split:
                     * <ins>A</ins><del>B</del>XY<ins>C</ins><del>D</del>
                     * <ins>A</ins>X<ins>C</ins><del>D</del>
                     * <ins>A</ins><del>B</del>X<ins>C</ins>
                     * <ins>A</del>X<ins>C</ins><del>D</del>
                     * <ins>A</ins><del>B</del>X<del>C</del>
                     */
                    if ((lastEquality.Length != 0)
                        && ((insertionBeforeLastEquality && deletionBeforeLastEquality && insertionAfterLastEquality && deletionAfterLastEquality)
                            || ((lastEquality.Length < diffEditCost / 2)
                                && (insertionBeforeLastEquality ? 1 : 0) + (deletionBeforeLastEquality ? 1 : 0) + (insertionAfterLastEquality ? 1 : 0)
                                + (deletionAfterLastEquality ? 1 : 0) == 3)))
                    {
                        diffs.Splice(equalities.Peek(), 1, Diff.Delete(lastEquality), Diff.Insert(lastEquality));
                        equalities.Pop();  // Throw away the equality we just deleted.
                        lastEquality = string.Empty;
                        if (insertionBeforeLastEquality && deletionBeforeLastEquality)
                        {
                            // No changes made which could affect previous entry, keep going.
                            insertionAfterLastEquality = deletionAfterLastEquality = true;
                            equalities.Clear();
                        }
                        else
                        {
                            if (equalities.Count > 0)
                            {
                                equalities.Pop();
                            }

                            i = equalities.Count > 0 ? equalities.Peek() : -1;
                            insertionAfterLastEquality = deletionAfterLastEquality = false;
                        }
                        changes = true;
                    }
                }
            }

            if (changes)
            {
                diffs.CleanupMerge();
            }

            return diffs;
        }

        /// <summary>
        /// Reduce the number of edits by eliminating semantically trivial equalities.
        /// </summary>
        /// <param name="diffs"></param>
        internal static List<Diff> CleanupSemantic(this List<Diff> diffs)
        {
            // Stack of indices where equalities are found.
            var equalities = new Stack<int>();
            // Always equal to equalities[equalitiesLength-1][1]
            string? lastEquality = null;
            var pointer = 0;  // Index of current position.
            // Number of characters that changed prior to the equality.
            var lengthInsertions1 = 0;
            var lengthDeletions1 = 0;
            // Number of characters that changed after the equality.
            var lengthInsertions2 = 0;
            var lengthDeletions2 = 0;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer].Operation == Equal)
                {  // Equality found.
                    equalities.Push(pointer);
                    lengthInsertions1 = lengthInsertions2;
                    lengthDeletions1 = lengthDeletions2;
                    lengthInsertions2 = 0;
                    lengthDeletions2 = 0;
                    lastEquality = diffs[pointer].Text;
                }
                else
                {  // an insertion or deletion
                    if (diffs[pointer].Operation == Insert)
                    {
                        lengthInsertions2 += diffs[pointer].Text.Length;
                    }
                    else
                    {
                        lengthDeletions2 += diffs[pointer].Text.Length;
                    }
                    // Eliminate an equality that is smaller or equal to the edits on both
                    // sides of it.
                    if (lastEquality != null 
                        && (lastEquality.Length <= Math.Max(lengthInsertions1, lengthDeletions1))
                        && (lastEquality.Length <= Math.Max(lengthInsertions2, lengthDeletions2)))
                    {
                        // Duplicate record.

                        diffs.Splice(equalities.Peek(), 1, Diff.Delete(lastEquality), Diff.Insert(lastEquality));

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
                        lastEquality = null;
                    }
                }
                pointer++;
            }

            diffs = diffs.CleanupMerge().CleanupSemanticLossless().ToList();

            // Find any overlaps between deletions and insertions.
            // e.g: <del>abcxxx</del><ins>xxxdef</ins>
            //   -> <del>abc</del>xxx<ins>def</ins>
            // e.g: <del>xxxabc</del><ins>defxxx</ins>
            //   -> <ins>def</ins>xxx<del>abc</del>
            // Only extract an overlap if it is as big as the edit ahead or behind it.
            pointer = 1;
            while (pointer < diffs.Count)
            {
                if (diffs[pointer - 1].Operation == Delete &&
                    diffs[pointer].Operation == Insert)
                {
                    var deletion = diffs[pointer - 1].Text.AsSpan();
                    var insertion = diffs[pointer].Text.AsSpan();
                    var overlapLength1 = TextUtil.CommonOverlap(deletion, insertion);
                    var overlapLength2 = TextUtil.CommonOverlap(insertion, deletion);
                    var minLength = Math.Min(deletion.Length, insertion.Length);

                    Diff[]? newdiffs = null;
                    if ((overlapLength1 >= overlapLength2) && (overlapLength1 >= minLength / 2.0))
                    {
                        // Overlap found.
                        // Insert an equality and trim the surrounding edits.
                        newdiffs = new[] 
                        {
                            Diff.Delete(deletion.Slice(0, deletion.Length - overlapLength1).ToArray()),
                            Diff.Equal(insertion.Slice(0, overlapLength1).ToArray()),
                            Diff.Insert(insertion[overlapLength1..].ToArray())
                        };
                    }
                    else if ((overlapLength2 >= overlapLength1) && overlapLength2 >= minLength / 2.0)
                    {
                        // Reverse overlap found.
                        // Insert an equality and swap and trim the surrounding edits.
                        newdiffs = new[]
                        {
                                Diff.Insert(insertion.Slice(0, insertion.Length - overlapLength2)),
                                Diff.Equal(deletion.Slice(0, overlapLength2)),
                                Diff.Delete(deletion[overlapLength2..])
                        };
                    }

                    if (newdiffs != null)
                    {
                        diffs.Splice(pointer - 1, 2, newdiffs);
                        pointer++;
                    }

                    pointer++;
                }
                pointer++;
            }
            return diffs;
        }


        /// <summary>
        /// Rehydrate the text in a diff from a string of line hashes to real lines of text.
        /// </summary>
        /// <param name="diffs"></param>
        /// <param name="lineArray">list of unique strings</param>
        /// <returns></returns>
        internal static IEnumerable<Diff> CharsToLines(this ICollection<Diff> diffs, IList<string> lineArray)
        {
            foreach (var diff in diffs)
            {
                var text = new StringBuilder();
                foreach (var c in diff.Text)
                {
                    text.Append(lineArray[c]);
                }
                yield return diff.Replace(text.ToString());
            }
        }

        /// <summary>
        /// Compute and return equivalent location in target text.
        /// </summary>
        /// <param name="diffs">list of diffs</param>
        /// <param name="location1">location in source</param>
        /// <returns>location in target</returns>
        internal static int FindEquivalentLocation2(this IEnumerable<Diff> diffs, int location1)
        {
            var chars1 = 0;
            var chars2 = 0;
            var lastChars1 = 0;
            var lastChars2 = 0;
            Diff lastDiff = Diff.Empty;
            foreach (var aDiff in diffs)
            {
                if (aDiff.Operation != Insert)
                {
                    // Equality or deletion.
                    chars1 += aDiff.Text.Length;
                }
                if (aDiff.Operation != Delete)
                {
                    // Equality or insertion.
                    chars2 += aDiff.Text.Length;
                }
                if (chars1 > location1)
                {
                    // Overshot the location.
                    lastDiff = aDiff;
                    break;
                }
                lastChars1 = chars1;
                lastChars2 = chars2;
            }
            if (lastDiff.Operation == Delete)
            {
                // The location was deleted.
                return lastChars2;
            }
            // Add the remaining character length.
            return lastChars2 + (location1 - lastChars1);
        }


    }
}