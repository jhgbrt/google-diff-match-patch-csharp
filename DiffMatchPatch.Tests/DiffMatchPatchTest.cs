/*
 * Copyright 2008 Google Inc. All Rights Reserved.
 * Author: fraser@google.com (Neil Fraser)
 * Author: anteru@developer.shelter13.net (Matthaeus G. Chajdas)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Diff Match and Patch -- Test Harness
 * http://code.google.com/p/google-diff-match-patch/
 */

using DiffMatchPatch;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nicTest
{
    [TestClass]
    public class diff_match_patchTest : diff_match_patch
    {
        [TestMethod]
        public void diff_halfmatchTest()
        {
            // No match.
            Assert.IsNull(TextUtil.HalfMatch("1234567890", "abcdef"));

            Assert.IsNull(TextUtil.HalfMatch("12345", "23"));

            // Single Match.
            CollectionAssert.AreEqual(new string[] { "12", "90", "a", "z", "345678" }, TextUtil.HalfMatch("1234567890", "a345678z"));

            CollectionAssert.AreEqual(new string[] { "a", "z", "12", "90", "345678" }, TextUtil.HalfMatch("a345678z", "1234567890"));

            CollectionAssert.AreEqual(new string[] { "abc", "z", "1234", "0", "56789" }, TextUtil.HalfMatch("abc56789z", "1234567890"));

            CollectionAssert.AreEqual(new string[] { "a", "xyz", "1", "7890", "23456" }, TextUtil.HalfMatch("a23456xyz", "1234567890"));

            // Multiple Matches.
            CollectionAssert.AreEqual(new string[] { "12123", "123121", "a", "z", "1234123451234" }, TextUtil.HalfMatch("121231234123451234123121", "a1234123451234z"));

            CollectionAssert.AreEqual(new string[] { "", "-=-=-=-=-=", "x", "", "x-=-=-=-=-=-=-=" }, TextUtil.HalfMatch("x-=-=-=-=-=-=-=-=-=-=-=-=", "xx-=-=-=-=-=-=-="));

            CollectionAssert.AreEqual(new string[] { "-=-=-=-=-=", "", "", "y", "-=-=-=-=-=-=-=y" }, TextUtil.HalfMatch("-=-=-=-=-=-=-=-=-=-=-=-=y", "-=-=-=-=-=-=-=yy"));

            // Non-optimal halfmatch.
            // Optimal diff would be -q+x=H-i+e=lloHe+Hu=llo-Hew+y not -qHillo+x=HelloHe-w+Hulloy
            CollectionAssert.AreEqual(new string[] { "qHillo", "w", "x", "Hulloy", "HelloHe" }, TextUtil.HalfMatch("qHilloHelloHew", "xHelloHeHulloy"));

        }

        [TestMethod]
        public void diff_charsToLinesTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Convert chars up to lines.
            List<Diff> diffs = new List<Diff> {
          Diff.EQUAL("\u0001\u0002\u0001"),
          Diff.INSERT("\u0002\u0001\u0002")};
            List<string> tmpVector = new List<string>();
            tmpVector.Add("");
            tmpVector.Add("alpha\n");
            tmpVector.Add("beta\n");
            diffs = dmp.diff_charsToLines(diffs, tmpVector).ToList();
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.EQUAL("alpha\nbeta\nalpha\n"),
          Diff.INSERT("beta\nalpha\nbeta\n")}, diffs);

            // More than 256 to reveal any 8-bit limitations.
            int n = 300;
            tmpVector.Clear();
            StringBuilder lineList = new StringBuilder();
            StringBuilder charList = new StringBuilder();
            for (int x = 1; x < n + 1; x++)
            {
                tmpVector.Add(x + "\n");
                lineList.Append(x + "\n");
                charList.Append(Convert.ToChar(x));
            }
            Assert.AreEqual(n, tmpVector.Count);
            string lines = lineList.ToString();
            string chars = charList.ToString();
            Assert.AreEqual(n, chars.Length);
            tmpVector.Insert(0, "");
            diffs = new List<Diff> { Diff.DELETE(chars) };
            diffs = dmp.diff_charsToLines(diffs, tmpVector).ToList();
            CollectionAssert.AreEqual(new List<Diff>
          {Diff.DELETE(lines)}, diffs);
        }

        [TestMethod]
        public void diff_cleanupMergeTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Cleanup a messy diff.
            // Null case.
            List<Diff> diffs = new List<Diff>();
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff>(), diffs);

            // No change case.
            diffs = new List<Diff> { Diff.EQUAL("a"), Diff.DELETE("b"), Diff.INSERT("c") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.EQUAL("a"), Diff.DELETE("b"), Diff.INSERT("c") }, diffs);

            // Merge equalities.
            diffs = new List<Diff> { Diff.EQUAL("a"), Diff.EQUAL("b"), Diff.EQUAL("c") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.EQUAL("abc") }, diffs);

            // Merge deletions.
            diffs = new List<Diff> { Diff.DELETE("a"), Diff.DELETE("b"), Diff.DELETE("c") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.DELETE("abc") }, diffs);

            // Merge insertions.
            diffs = new List<Diff> { Diff.INSERT("a"), Diff.INSERT("b"), Diff.INSERT("c") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.INSERT("abc") }, diffs);

            // Merge interweave.
            diffs = new List<Diff> { Diff.DELETE("a"), Diff.INSERT("b"), Diff.DELETE("c"), Diff.INSERT("d"), Diff.EQUAL("e"), Diff.EQUAL("f") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.DELETE("ac"), Diff.INSERT("bd"), Diff.EQUAL("ef") }, diffs);

            // Prefix and suffix detection.
            diffs = new List<Diff> { Diff.DELETE("a"), Diff.INSERT("abc"), Diff.DELETE("dc") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.EQUAL("a"), Diff.DELETE("d"), Diff.INSERT("b"), Diff.EQUAL("c") }, diffs);

            // Prefix and suffix detection with equalities.
            diffs = new List<Diff> { Diff.EQUAL("x"), Diff.DELETE("a"), Diff.INSERT("abc"), Diff.DELETE("dc"), Diff.EQUAL("y") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.EQUAL("xa"), Diff.DELETE("d"), Diff.INSERT("b"), Diff.EQUAL("cy") }, diffs);

            // Slide edit left.
            diffs = new List<Diff> { Diff.EQUAL("a"), Diff.INSERT("ba"), Diff.EQUAL("c") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.INSERT("ab"), Diff.EQUAL("ac") }, diffs);

            // Slide edit right.
            diffs = new List<Diff> { Diff.EQUAL("c"), Diff.INSERT("ab"), Diff.EQUAL("a") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.EQUAL("ca"), Diff.INSERT("ba") }, diffs);

            // Slide edit left recursive.
            diffs = new List<Diff> { Diff.EQUAL("a"), Diff.DELETE("b"), Diff.EQUAL("c"), Diff.DELETE("ac"), Diff.EQUAL("x") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.DELETE("abc"), Diff.EQUAL("acx") }, diffs);

            // Slide edit right recursive.
            diffs = new List<Diff> { Diff.EQUAL("x"), Diff.DELETE("ca"), Diff.EQUAL("c"), Diff.DELETE("b"), Diff.EQUAL("a") };
            diff_cleanupMerge(diffs);
            CollectionAssert.AreEqual(new List<Diff> { Diff.EQUAL("xca"), Diff.DELETE("cba") }, diffs);
        }

        [TestMethod]
        public void diff_cleanupSemanticLosslessTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Slide diffs to match logical boundaries.
            // Null case.
            List<Diff> diffs = new List<Diff>();
            diff_cleanupSemanticLossless(diffs);
            CollectionAssert.AreEqual(new List<Diff>(), diffs);

            // Blank lines.
            diffs = new List<Diff> {
          Diff.EQUAL("AAA\r\n\r\nBBB"),
          Diff.INSERT("\r\nDDD\r\n\r\nBBB"),
          Diff.EQUAL("\r\nEEE")
      };
            dmp.diff_cleanupSemanticLossless(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.EQUAL("AAA\r\n\r\n"),
          Diff.INSERT("BBB\r\nDDD\r\n\r\n"),
          Diff.EQUAL("BBB\r\nEEE")}, diffs);

            // Line boundaries.
            diffs = new List<Diff> {
          Diff.EQUAL("AAA\r\nBBB"),
          Diff.INSERT(" DDD\r\nBBB"),
          Diff.EQUAL(" EEE")};
            dmp.diff_cleanupSemanticLossless(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.EQUAL("AAA\r\n"),
          Diff.INSERT("BBB DDD\r\n"),
          Diff.EQUAL("BBB EEE")}, diffs);

            // Word boundaries.
            diffs = new List<Diff> {
          Diff.EQUAL("The c"),
          Diff.INSERT("ow and the c"),
          Diff.EQUAL("at.")};
            dmp.diff_cleanupSemanticLossless(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.EQUAL("The "),
          Diff.INSERT("cow and the "),
          Diff.EQUAL("cat.")}, diffs);

            // Alphanumeric boundaries.
            diffs = new List<Diff> {
          Diff.EQUAL("The-c"),
          Diff.INSERT("ow-and-the-c"),
          Diff.EQUAL("at.")};
            dmp.diff_cleanupSemanticLossless(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.EQUAL("The-"),
          Diff.INSERT("cow-and-the-"),
          Diff.EQUAL("cat.")}, diffs);

            // Hitting the start.
            diffs = new List<Diff> {
          Diff.EQUAL("a"),
          Diff.DELETE("a"),
          Diff.EQUAL("ax")};
            dmp.diff_cleanupSemanticLossless(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("a"),
          Diff.EQUAL("aax")}, diffs);

            // Hitting the end.
            diffs = new List<Diff> {
          Diff.EQUAL("xa"),
          Diff.DELETE("a"),
          Diff.EQUAL("a")};
            dmp.diff_cleanupSemanticLossless(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.EQUAL("xaa"),
          Diff.DELETE("a")}, diffs);

            // Sentence boundaries.
            diffs = new List<Diff> {
          Diff.EQUAL("The xxx. The "),
          Diff.INSERT("zzz. The "),
          Diff.EQUAL("yyy.")};
            dmp.diff_cleanupSemanticLossless(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.EQUAL("The xxx."),
          Diff.INSERT(" The zzz."),
          Diff.EQUAL(" The yyy.")}, diffs);
        }

        [TestMethod]
        public void diff_cleanupSemanticTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Cleanup semantically trivial equalities.
            // Null case.
            List<Diff> diffs = new List<Diff>();
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff>(), diffs);

            // No elimination #1.
            diffs = new List<Diff> {
          Diff.DELETE("ab"),
          Diff.INSERT("cd"),
          Diff.EQUAL("12"),
          Diff.DELETE("e")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("ab"),
          Diff.INSERT("cd"),
          Diff.EQUAL("12"),
          Diff.DELETE("e")}, diffs);

            // No elimination #2.
            diffs = new List<Diff> {
          Diff.DELETE("abc"),
          Diff.INSERT("ABC"),
          Diff.EQUAL("1234"),
          Diff.DELETE("wxyz")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abc"),
          Diff.INSERT("ABC"),
          Diff.EQUAL("1234"),
          Diff.DELETE("wxyz")}, diffs);

            // Simple elimination.
            diffs = new List<Diff> {
          Diff.DELETE("a"),
          Diff.EQUAL("b"),
          Diff.DELETE("c")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abc"),
          Diff.INSERT("b")}, diffs);

            // Backpass elimination.
            diffs = new List<Diff> {
          Diff.DELETE("ab"),
          Diff.EQUAL("cd"),
          Diff.DELETE("e"),
          Diff.EQUAL("f"),
          Diff.INSERT("g")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abcdef"),
          Diff.INSERT("cdfg")}, diffs);

            // Multiple eliminations.
            diffs = new List<Diff> {
          Diff.INSERT("1"),
          Diff.EQUAL("A"),
          Diff.DELETE("B"),
          Diff.INSERT("2"),
          Diff.EQUAL("_"),
          Diff.INSERT("1"),
          Diff.EQUAL("A"),
          Diff.DELETE("B"),
          Diff.INSERT("2")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("AB_AB"),
          Diff.INSERT("1A2_1A2")}, diffs);

            // Word boundaries.
            diffs = new List<Diff> {
          Diff.EQUAL("The c"),
          Diff.DELETE("ow and the c"),
          Diff.EQUAL("at.")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.EQUAL("The "),
          Diff.DELETE("cow and the "),
          Diff.EQUAL("cat.")}, diffs);

            // No overlap elimination.
            diffs = new List<Diff> {
          Diff.DELETE("abcxx"),
          Diff.INSERT("xxdef")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abcxx"),
          Diff.INSERT("xxdef")}, diffs);

            // Overlap elimination.
            diffs = new List<Diff> {
          Diff.DELETE("abcxxx"),
          Diff.INSERT("xxxdef")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abc"),
          Diff.EQUAL("xxx"),
          Diff.INSERT("def")}, diffs);

            // Reverse overlap elimination.
            diffs = new List<Diff> {
          Diff.DELETE("xxxabc"),
          Diff.INSERT("defxxx")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.INSERT("def"),
          Diff.EQUAL("xxx"),
          Diff.DELETE("abc")}, diffs);

            // Two overlap eliminations.
            diffs = new List<Diff> {
          Diff.DELETE("abcd1212"),
          Diff.INSERT("1212efghi"),
          Diff.EQUAL("----"),
          Diff.DELETE("A3"),
          Diff.INSERT("3BC")};
            dmp.diff_cleanupSemantic(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abcd"),
          Diff.EQUAL("1212"),
          Diff.INSERT("efghi"),
          Diff.EQUAL("----"),
          Diff.DELETE("A"),
          Diff.EQUAL("3"),
          Diff.INSERT("BC")}, diffs);
        }

        [TestMethod]
        public void diff_cleanupEfficiencyTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Cleanup operationally trivial equalities.
            dmp.DiffEditCost = 4;
            // Null case.
            List<Diff> diffs = new List<Diff>();
            dmp.diff_cleanupEfficiency(diffs);
            CollectionAssert.AreEqual(new List<Diff>(), diffs);

            // No elimination.
            diffs = new List<Diff> {
          Diff.DELETE("ab"),
          Diff.INSERT("12"),
          Diff.EQUAL("wxyz"),
          Diff.DELETE("cd"),
          Diff.INSERT("34")};
            dmp.diff_cleanupEfficiency(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("ab"),
          Diff.INSERT("12"),
          Diff.EQUAL("wxyz"),
          Diff.DELETE("cd"),
          Diff.INSERT("34")}, diffs);

            // Four-edit elimination.
            diffs = new List<Diff> {
          Diff.DELETE("ab"),
          Diff.INSERT("12"),
          Diff.EQUAL("xyz"),
          Diff.DELETE("cd"),
          Diff.INSERT("34")};
            dmp.diff_cleanupEfficiency(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abxyzcd"),
          Diff.INSERT("12xyz34")}, diffs);

            // Three-edit elimination.
            diffs = new List<Diff> {
          Diff.INSERT("12"),
          Diff.EQUAL("x"),
          Diff.DELETE("cd"),
          Diff.INSERT("34")};
            dmp.diff_cleanupEfficiency(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("xcd"),
          Diff.INSERT("12x34")}, diffs);

            // Backpass elimination.
            diffs = new List<Diff> {
          Diff.DELETE("ab"),
          Diff.INSERT("12"),
          Diff.EQUAL("xy"),
          Diff.INSERT("34"),
          Diff.EQUAL("z"),
          Diff.DELETE("cd"),
          Diff.INSERT("56")};
            dmp.diff_cleanupEfficiency(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abxyzcd"),
          Diff.INSERT("12xy34z56")}, diffs);

            // High cost elimination.
            dmp.DiffEditCost = 5;
            diffs = new List<Diff> {
          Diff.DELETE("ab"),
          Diff.INSERT("12"),
          Diff.EQUAL("wxyz"),
          Diff.DELETE("cd"),
          Diff.INSERT("34")};
            dmp.diff_cleanupEfficiency(diffs);
            CollectionAssert.AreEqual(new List<Diff> {
          Diff.DELETE("abwxyzcd"),
          Diff.INSERT("12wxyz34")}, diffs);
            dmp.DiffEditCost = 4;
        }

        [TestMethod]
        public void diff_prettyHtmlTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Pretty print.
            List<Diff> diffs = new List<Diff> {
          Diff.EQUAL("a\n"),
          Diff.DELETE("<B>b</B>"),
          Diff.INSERT("c&d")};
            Assert.AreEqual("<span>a&para;<br></span><del style=\"background:#ffe6e6;\">&lt;B&gt;b&lt;/B&gt;</del><ins style=\"background:#e6ffe6;\">c&amp;d</ins>",
                dmp.diff_prettyHtml(diffs));
        }

        [TestMethod]
        public void diff_textTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Compute the source and destination texts.
            List<Diff> diffs = new List<Diff> {
          Diff.EQUAL("jump"),
          Diff.DELETE("s"),
          Diff.INSERT("ed"),
          Diff.EQUAL(" over "),
          Diff.DELETE("the"),
          Diff.INSERT("a"),
          Diff.EQUAL(" lazy")};
            Assert.AreEqual("jumps over the lazy", dmp.diff_text1(diffs));

            Assert.AreEqual("jumped over a lazy", dmp.diff_text2(diffs));
        }

        [TestMethod]
        public void diff_deltaTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Convert a diff into delta string.
            List<Diff> diffs = new List<Diff> {
          Diff.EQUAL("jump"),
          Diff.DELETE("s"),
          Diff.INSERT("ed"),
          Diff.EQUAL(" over "),
          Diff.DELETE("the"),
          Diff.INSERT("a"),
          Diff.EQUAL(" lazy"),
          Diff.INSERT("old dog")};
            string text1 = dmp.diff_text1(diffs);
            Assert.AreEqual("jumps over the lazy", text1);

            string delta = dmp.diff_toDelta(diffs);
            Assert.AreEqual("=4\t-1\t+ed\t=6\t-3\t+a\t=5\t+old dog", delta);

            // Convert delta string into a diff.
            CollectionAssert.AreEqual(diffs, dmp.diff_fromDelta(text1, delta));

            // Generates error (19 < 20).
            try
            {
                dmp.diff_fromDelta(text1 + "x", delta);
                Assert.Fail("diff_fromDelta: Too long.");
            }
            catch (ArgumentException)
            {
                // Exception expected.
            }

            // Generates error (19 > 18).
            try
            {
                dmp.diff_fromDelta(text1.Substring(1), delta);
                Assert.Fail("diff_fromDelta: Too short.");
            }
            catch (ArgumentException)
            {
                // Exception expected.
            }

            // Generates error (%c3%xy invalid Unicode).
            try
            {
                dmp.diff_fromDelta("", "+%c3%xy");
                Assert.Fail("diff_fromDelta: Invalid character.");
            }
            catch (ArgumentException)
            {
                // Exception expected.
            }

            // Test deltas with special characters.
            char zero = (char)0;
            char one = (char)1;
            char two = (char)2;
            diffs = new List<Diff> {
          Diff.EQUAL("\u0680 " + zero + " \t %"),
          Diff.DELETE("\u0681 " + one + " \n ^"),
          Diff.INSERT("\u0682 " + two + " \\ |")};
            text1 = dmp.diff_text1(diffs);
            Assert.AreEqual("\u0680 " + zero + " \t %\u0681 " + one + " \n ^", text1);

            delta = dmp.diff_toDelta(diffs);
            // Lowercase, due to UrlEncode uses lower.
            Assert.AreEqual("=7\t-7\t+%da%82 %02 %5c %7c", delta, "diff_toDelta: Unicode.");

            CollectionAssert.AreEqual(diffs, dmp.diff_fromDelta(text1, delta), "diff_fromDelta: Unicode.");

            // Verify pool of unchanged characters.
            diffs = new List<Diff> {
          Diff.INSERT("A-Z a-z 0-9 - _ . ! ~ * ' ( ) ; / ? : @ & = + $ , # ")};
            string text2 = dmp.diff_text2(diffs);
            Assert.AreEqual("A-Z a-z 0-9 - _ . ! ~ * \' ( ) ; / ? : @ & = + $ , # ", text2, "diff_text2: Unchanged characters.");

            delta = dmp.diff_toDelta(diffs);
            Assert.AreEqual("+A-Z a-z 0-9 - _ . ! ~ * \' ( ) ; / ? : @ & = + $ , # ", delta, "diff_toDelta: Unchanged characters.");

            // Convert delta string into a diff.
            CollectionAssert.AreEqual(diffs, dmp.diff_fromDelta("", delta), "diff_fromDelta: Unchanged characters.");
        }

        [TestMethod]
        public void diff_xIndexTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Translate a location in text1 to text2.
            List<Diff> diffs = new List<Diff> {
          Diff.DELETE("a"),
          Diff.INSERT("1234"),
          Diff.EQUAL("xyz")};
            Assert.AreEqual(5, dmp.diff_xIndex(diffs, 2), "diff_xIndex: Translation on equality.");

            diffs = new List<Diff> {
          Diff.EQUAL("a"),
          Diff.DELETE("1234"),
          Diff.EQUAL("xyz")};
            Assert.AreEqual(1, dmp.diff_xIndex(diffs, 3), "diff_xIndex: Translation on deletion.");
        }

        [TestMethod]
        public void diff_levenshteinTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            List<Diff> diffs = new List<Diff> {
          Diff.DELETE("abc"),
          Diff.INSERT("1234"),
          Diff.EQUAL("xyz")};
            Assert.AreEqual(4, dmp.diff_levenshtein(diffs), "diff_levenshtein: Levenshtein with trailing equality.");

            diffs = new List<Diff> {
          Diff.EQUAL("xyz"),
          Diff.DELETE("abc"),
          Diff.INSERT("1234")};
            Assert.AreEqual(4, dmp.diff_levenshtein(diffs), "diff_levenshtein: Levenshtein with leading equality.");

            diffs = new List<Diff> {
          Diff.DELETE("abc"),
          Diff.EQUAL("xyz"),
          Diff.INSERT("1234")};
            Assert.AreEqual(7, dmp.diff_levenshtein(diffs), "diff_levenshtein: Levenshtein with middle equality.");
        }

        [TestMethod]
        public void diff_bisectTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Normal.
            string a = "cat";
            string b = "map";
            // Since the resulting diff hasn't been normalized, it would be ok if
            // the insertion and deletion pairs are swapped.
            // If the order changes, tweak this test as required.
            List<Diff> diffs = new List<Diff> { Diff.DELETE("c"), Diff.INSERT("m"), Diff.EQUAL("a"), Diff.DELETE("t"), Diff.INSERT("p") };
            CollectionAssert.AreEqual(diffs, dmp.diff_bisect(a, b, DateTime.MaxValue));

            // Timeout.
            diffs = new List<Diff> { Diff.DELETE("cat"), Diff.INSERT("map") };
            CollectionAssert.AreEqual(diffs, dmp.diff_bisect(a, b, DateTime.MinValue));
        }

        [TestMethod]
        public void diff_mainTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Perform a trivial diff.
            List<Diff> diffs = new List<Diff> { };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("", "", false), "diff_main: Null case.");

            diffs = new List<Diff> { Diff.EQUAL("abc") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("abc", "abc", false), "diff_main: Equality.");

            diffs = new List<Diff> { Diff.EQUAL("ab"), Diff.INSERT("123"), Diff.EQUAL("c") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("abc", "ab123c", false), "diff_main: Simple insertion.");

            diffs = new List<Diff> { Diff.EQUAL("a"), Diff.DELETE("123"), Diff.EQUAL("bc") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("a123bc", "abc", false), "diff_main: Simple deletion.");

            diffs = new List<Diff> { Diff.EQUAL("a"), Diff.INSERT("123"), Diff.EQUAL("b"), Diff.INSERT("456"), Diff.EQUAL("c") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("abc", "a123b456c", false), "diff_main: Two insertions.");

            diffs = new List<Diff> { Diff.EQUAL("a"), Diff.DELETE("123"), Diff.EQUAL("b"), Diff.DELETE("456"), Diff.EQUAL("c") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("a123b456c", "abc", false), "diff_main: Two deletions.");

            // Perform a real diff.
            // Switch off the timeout.
            dmp.DiffTimeout = 0;
            diffs = new List<Diff> { Diff.DELETE("a"), Diff.INSERT("b") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("a", "b", false), "diff_main: Simple case #1.");

            diffs = new List<Diff> { Diff.DELETE("Apple"), Diff.INSERT("Banana"), Diff.EQUAL("s are a"), Diff.INSERT("lso"), Diff.EQUAL(" fruit.") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("Apples are a fruit.", "Bananas are also fruit.", false), "diff_main: Simple case #2.");

            diffs = new List<Diff> { Diff.DELETE("a"), Diff.INSERT("\u0680"), Diff.EQUAL("x"), Diff.DELETE("\t"), Diff.INSERT(new string(new char[] { (char)0 })) };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("ax\t", "\u0680x" + (char)0, false), "diff_main: Simple case #3.");

            diffs = new List<Diff> { Diff.DELETE("1"), Diff.EQUAL("a"), Diff.DELETE("y"), Diff.EQUAL("b"), Diff.DELETE("2"), Diff.INSERT("xab") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("1ayb2", "abxab", false), "diff_main: Overlap #1.");

            diffs = new List<Diff> { Diff.INSERT("xaxcx"), Diff.EQUAL("abc"), Diff.DELETE("y") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("abcy", "xaxcxabc", false), "diff_main: Overlap #2.");

            diffs = new List<Diff> { Diff.DELETE("ABCD"), Diff.EQUAL("a"), Diff.DELETE("="), Diff.INSERT("-"), Diff.EQUAL("bcd"), Diff.DELETE("="), Diff.INSERT("-"), Diff.EQUAL("efghijklmnopqrs"), Diff.DELETE("EFGHIJKLMNOefg") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("ABCDa=bcd=efghijklmnopqrsEFGHIJKLMNOefg", "a-bcd-efghijklmnopqrs", false), "diff_main: Overlap #3.");

            diffs = new List<Diff> { Diff.INSERT(" "), Diff.EQUAL("a"), Diff.INSERT("nd"), Diff.EQUAL(" [[Pennsylvania]]"), Diff.DELETE(" and [[New") };
            CollectionAssert.AreEqual(diffs, dmp.diff_main("a [[Pennsylvania]] and [[New", " and [[Pennsylvania]]", false), "diff_main: Large equality.");

            dmp.DiffTimeout = 0.1f;  // 100ms
            string a = "`Twas brillig, and the slithy toves\nDid gyre and gimble in the wabe:\nAll mimsy were the borogoves,\nAnd the mome raths outgrabe.\n";
            string b = "I am the very model of a modern major general,\nI've information vegetable, animal, and mineral,\nI know the kings of England, and I quote the fights historical,\nFrom Marathon to Waterloo, in order categorical.\n";
            // Increase the text lengths by 1024 times to ensure a timeout.
            for (int x = 0; x < 10; x++)
            {
                a = a + a;
                b = b + b;
            }
            DateTime startTime = DateTime.Now;
            dmp.diff_main(a, b);
            DateTime endTime = DateTime.Now;
            // Test that we took at least the timeout period.
            Assert.IsTrue(new TimeSpan(((long)(dmp.DiffTimeout * 1000)) * 10000) <= endTime - startTime);
            // Test that we didn't take forever (be forgiving).
            // Theoretically this test could fail very occasionally if the
            // OS task swaps or locks up for a second at the wrong moment.
            Assert.IsTrue(new TimeSpan(((long)(dmp.DiffTimeout * 1000)) * 10000 * 2) > endTime - startTime);
            dmp.DiffTimeout = 0;

            // Test the linemode speedup.
            // Must be long to pass the 100 char cutoff.
            a = "1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n";
            b = "abcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\n";
            CollectionAssert.AreEqual(dmp.diff_main(a, b, true), dmp.diff_main(a, b, false), "diff_main: Simple line-mode.");

            a = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
            b = "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij";
            CollectionAssert.AreEqual(dmp.diff_main(a, b, true), dmp.diff_main(a, b, false), "diff_main: Single line-mode.");

            a = "1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n";
            b = "abcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n";
            string[] texts_linemode = diff_rebuildtexts(dmp.diff_main(a, b, true));
            string[] texts_textmode = diff_rebuildtexts(dmp.diff_main(a, b, false));
            CollectionAssert.AreEqual(texts_textmode, texts_linemode, "diff_main: Overlap line-mode.");

            // Test null inputs -- not needed because nulls can't be passed in C#.
        }

        [TestMethod]
        public void match_alphabetTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Initialise the bitmasks for Bitap.
            Dictionary<char, int> bitmask = new Dictionary<char, int>();
            bitmask.Add('a', 4); bitmask.Add('b', 2); bitmask.Add('c', 1);
            CollectionAssert.AreEqual(bitmask, dmp.match_alphabet("abc"), "match_alphabet: Unique.");

            bitmask.Clear();
            bitmask.Add('a', 37); bitmask.Add('b', 18); bitmask.Add('c', 8);
            CollectionAssert.AreEqual(bitmask, dmp.match_alphabet("abcaba"), "match_alphabet: Duplicates.");
        }

        [TestMethod]
        public void match_bitapTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();

            // Bitap algorithm.
            dmp.MatchDistance = 100;
            dmp.MatchThreshold = 0.5f;
            Assert.AreEqual(5, dmp.match_bitap("abcdefghijk", "fgh", 5), "match_bitap: Exact match #1.");

            Assert.AreEqual(5, dmp.match_bitap("abcdefghijk", "fgh", 0), "match_bitap: Exact match #2.");

            Assert.AreEqual(4, dmp.match_bitap("abcdefghijk", "efxhi", 0), "match_bitap: Fuzzy match #1.");

            Assert.AreEqual(2, dmp.match_bitap("abcdefghijk", "cdefxyhijk", 5), "match_bitap: Fuzzy match #2.");

            Assert.AreEqual(-1, dmp.match_bitap("abcdefghijk", "bxy", 1), "match_bitap: Fuzzy match #3.");

            Assert.AreEqual(2, dmp.match_bitap("123456789xx0", "3456789x0", 2), "match_bitap: Overflow.");

            Assert.AreEqual(0, dmp.match_bitap("abcdef", "xxabc", 4), "match_bitap: Before start match.");

            Assert.AreEqual(3, dmp.match_bitap("abcdef", "defyy", 4), "match_bitap: Beyond end match.");

            Assert.AreEqual(0, dmp.match_bitap("abcdef", "xabcdefy", 0), "match_bitap: Oversized pattern.");

            dmp.MatchThreshold = 0.4f;
            Assert.AreEqual(4, dmp.match_bitap("abcdefghijk", "efxyhi", 1), "match_bitap: Threshold #1.");

            dmp.MatchThreshold = 0.3f;
            Assert.AreEqual(-1, dmp.match_bitap("abcdefghijk", "efxyhi", 1), "match_bitap: Threshold #2.");

            dmp.MatchThreshold = 0.0f;
            Assert.AreEqual(1, dmp.match_bitap("abcdefghijk", "bcdef", 1), "match_bitap: Threshold #3.");

            dmp.MatchThreshold = 0.5f;
            Assert.AreEqual(0, dmp.match_bitap("abcdexyzabcde", "abccde", 3), "match_bitap: Multiple select #1.");

            Assert.AreEqual(8, dmp.match_bitap("abcdexyzabcde", "abccde", 5), "match_bitap: Multiple select #2.");

            dmp.MatchDistance = 10;  // Strict location.
            Assert.AreEqual(-1, dmp.match_bitap("abcdefghijklmnopqrstuvwxyz", "abcdefg", 24), "match_bitap: Distance test #1.");

            Assert.AreEqual(0, dmp.match_bitap("abcdefghijklmnopqrstuvwxyz", "abcdxxefg", 1), "match_bitap: Distance test #2.");

            dmp.MatchDistance = 1000;  // Loose location.
            Assert.AreEqual(0, dmp.match_bitap("abcdefghijklmnopqrstuvwxyz", "abcdefg", 24), "match_bitap: Distance test #3.");
        }

        [TestMethod]
        public void match_mainTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            // Full match.
            Assert.AreEqual(0, dmp.match_main("abcdef", "abcdef", 1000), "match_main: Equality.");

            Assert.AreEqual(-1, dmp.match_main("", "abcdef", 1), "match_main: Null text.");

            Assert.AreEqual(3, dmp.match_main("abcdef", "", 3), "match_main: Null pattern.");

            Assert.AreEqual(3, dmp.match_main("abcdef", "de", 3), "match_main: Exact match.");

            Assert.AreEqual(3, dmp.match_main("abcdef", "defy", 4), "match_main: Beyond end match.");

            Assert.AreEqual(0, dmp.match_main("abcdef", "abcdefy", 0), "match_main: Oversized pattern.");

            dmp.MatchThreshold = 0.7f;
            Assert.AreEqual(4, dmp.match_main("I am the very model of a modern major general.", " that berry ", 5), "match_main: Complex match.");
            dmp.MatchThreshold = 0.5f;

            // Test null inputs -- not needed because nulls can't be passed in C#.
        }

        [TestMethod]
        public void patch_patchObjTest()
        {
            // Patch Object.
            Patch p = new Patch();
            p.start1 = 20;
            p.start2 = 21;
            p.length1 = 18;
            p.length2 = 17;
            p.diffs = new List<Diff> {
          Diff.EQUAL("jump"),
          Diff.DELETE("s"),
          Diff.INSERT("ed"),
          Diff.EQUAL(" over "),
          Diff.DELETE("the"),
          Diff.INSERT("a"),
          Diff.EQUAL("\nlaz")};
            string strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n %0alaz\n";
            Assert.AreEqual(strp, p.ToString(), "Patch: toString.");
        }

        [TestMethod]
        public void patch_fromTextTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            Assert.IsTrue(dmp.patch_fromText("").Count == 0, "patch_fromText: #0.");

            string strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n %0alaz\n";
            Assert.AreEqual(strp, dmp.patch_fromText(strp)[0].ToString(), "patch_fromText: #1.");

            Assert.AreEqual("@@ -1 +1 @@\n-a\n+b\n", dmp.patch_fromText("@@ -1 +1 @@\n-a\n+b\n")[0].ToString(), "patch_fromText: #2.");

            Assert.AreEqual("@@ -1,3 +0,0 @@\n-abc\n", dmp.patch_fromText("@@ -1,3 +0,0 @@\n-abc\n")[0].ToString(), "patch_fromText: #3.");

            Assert.AreEqual("@@ -0,0 +1,3 @@\n+abc\n", dmp.patch_fromText("@@ -0,0 +1,3 @@\n+abc\n")[0].ToString(), "patch_fromText: #4.");

            // Generates error.
            try
            {
                dmp.patch_fromText("Bad\nPatch\n");
                Assert.Fail("patch_fromText: #5.");
            }
            catch (ArgumentException)
            {
                // Exception expected.
            }
        }

        [TestMethod]
        public void patch_toTextTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            string strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n  laz\n";
            List<Patch> patches;
            patches = dmp.patch_fromText(strp);
            string result = dmp.patch_toText(patches);
            Assert.AreEqual(strp, result);

            strp = "@@ -1,9 +1,9 @@\n-f\n+F\n oo+fooba\n@@ -7,9 +7,9 @@\n obar\n-,\n+.\n  tes\n";
            patches = dmp.patch_fromText(strp);
            result = dmp.patch_toText(patches);
            Assert.AreEqual(strp, result);
        }

        [TestMethod]
        public void patch_addContextTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            dmp.PatchMargin = 4;
            Patch p;
            p = dmp.patch_fromText("@@ -21,4 +21,10 @@\n-jump\n+somersault\n")[0];
            dmp.patch_addContext(p, "The quick brown fox jumps over the lazy dog.");
            Assert.AreEqual("@@ -17,12 +17,18 @@\n fox \n-jump\n+somersault\n s ov\n", p.ToString(), "patch_addContext: Simple case.");

            p = dmp.patch_fromText("@@ -21,4 +21,10 @@\n-jump\n+somersault\n")[0];
            dmp.patch_addContext(p, "The quick brown fox jumps.");
            Assert.AreEqual("@@ -17,10 +17,16 @@\n fox \n-jump\n+somersault\n s.\n", p.ToString(), "patch_addContext: Not enough trailing context.");

            p = dmp.patch_fromText("@@ -3 +3,2 @@\n-e\n+at\n")[0];
            dmp.patch_addContext(p, "The quick brown fox jumps.");
            Assert.AreEqual("@@ -1,7 +1,8 @@\n Th\n-e\n+at\n  qui\n", p.ToString(), "patch_addContext: Not enough leading context.");

            p = dmp.patch_fromText("@@ -3 +3,2 @@\n-e\n+at\n")[0];
            dmp.patch_addContext(p, "The quick brown fox jumps.  The quick brown fox crashes.");
            Assert.AreEqual("@@ -1,27 +1,28 @@\n Th\n-e\n+at\n  quick brown fox jumps. \n", p.ToString(), "patch_addContext: Ambiguity.");
        }

        [TestMethod]
        public void patch_makeTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            List<Patch> patches;
            patches = dmp.patch_make("", "");
            Assert.AreEqual("", dmp.patch_toText(patches), "patch_make: Null case.");

            string text1 = "The quick brown fox jumps over the lazy dog.";
            string text2 = "That quick brown fox jumped over a lazy dog.";
            string expectedPatch = "@@ -1,8 +1,7 @@\n Th\n-at\n+e\n  qui\n@@ -21,17 +21,18 @@\n jump\n-ed\n+s\n  over \n-a\n+the\n  laz\n";
            // The second patch must be "-21,17 +21,18", not "-22,17 +21,18" due to rolling context.
            patches = dmp.patch_make(text2, text1);
            Assert.AreEqual(expectedPatch, dmp.patch_toText(patches), "patch_make: Text2+Text1 inputs.");

            expectedPatch = "@@ -1,11 +1,12 @@\n Th\n-e\n+at\n  quick b\n@@ -22,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n  laz\n";
            patches = dmp.patch_make(text1, text2);
            Assert.AreEqual(expectedPatch, dmp.patch_toText(patches), "patch_make: Text1+Text2 inputs.");

            List<Diff> diffs = dmp.diff_main(text1, text2, false);
            patches = dmp.patch_make(diffs);
            Assert.AreEqual(expectedPatch, dmp.patch_toText(patches), "patch_make: Diff input.");

            patches = dmp.patch_make(text1, diffs);
            Assert.AreEqual(expectedPatch, dmp.patch_toText(patches), "patch_make: Text1+Diff inputs.");

            patches = dmp.patch_make(text1, text2, diffs);
            Assert.AreEqual(expectedPatch, dmp.patch_toText(patches), "patch_make: Text1+Text2+Diff inputs (deprecated).");

            patches = dmp.patch_make("`1234567890-=[]\\;',./", "~!@#$%^&*()_+{}|:\"<>?");
            Assert.AreEqual("@@ -1,21 +1,21 @@\n-%601234567890-=%5b%5d%5c;',./\n+~!@#$%25%5e&*()_+%7b%7d%7c:%22%3c%3e?\n",
                dmp.patch_toText(patches),
                "patch_toText: Character encoding.");

            diffs = new List<Diff> {
          Diff.DELETE("`1234567890-=[]\\;',./"),
          Diff.INSERT("~!@#$%^&*()_+{}|:\"<>?")};
            CollectionAssert.AreEqual(diffs,
                dmp.patch_fromText("@@ -1,21 +1,21 @@\n-%601234567890-=%5B%5D%5C;',./\n+~!@#$%25%5E&*()_+%7B%7D%7C:%22%3C%3E?\n")[0].diffs,
                "patch_fromText: Character decoding.");

            text1 = "";
            for (int x = 0; x < 100; x++)
            {
                text1 += "abcdef";
            }
            text2 = text1 + "123";
            expectedPatch = "@@ -573,28 +573,31 @@\n cdefabcdefabcdefabcdefabcdef\n+123\n";
            patches = dmp.patch_make(text1, text2);
            Assert.AreEqual(expectedPatch, dmp.patch_toText(patches), "patch_make: Long string with repeats.");

            // Test null inputs -- not needed because nulls can't be passed in C#.
        }

        [TestMethod]
        public void patch_splitMaxTest()
        {
            // Assumes that Match_MaxBits is 32.
            diff_match_patchTest dmp = new diff_match_patchTest();
            List<Patch> patches;

            patches = dmp.patch_make("abcdefghijklmnopqrstuvwxyz01234567890", "XabXcdXefXghXijXklXmnXopXqrXstXuvXwxXyzX01X23X45X67X89X0");
            dmp.patch_splitMax(patches);
            Assert.AreEqual("@@ -1,32 +1,46 @@\n+X\n ab\n+X\n cd\n+X\n ef\n+X\n gh\n+X\n ij\n+X\n kl\n+X\n mn\n+X\n op\n+X\n qr\n+X\n st\n+X\n uv\n+X\n wx\n+X\n yz\n+X\n 012345\n@@ -25,13 +39,18 @@\n zX01\n+X\n 23\n+X\n 45\n+X\n 67\n+X\n 89\n+X\n 0\n", dmp.patch_toText(patches));

            patches = dmp.patch_make("abcdef1234567890123456789012345678901234567890123456789012345678901234567890uvwxyz", "abcdefuvwxyz");
            string oldToText = dmp.patch_toText(patches);
            dmp.patch_splitMax(patches);
            Assert.AreEqual(oldToText, dmp.patch_toText(patches));

            patches = dmp.patch_make("1234567890123456789012345678901234567890123456789012345678901234567890", "abc");
            dmp.patch_splitMax(patches);
            Assert.AreEqual("@@ -1,32 +1,4 @@\n-1234567890123456789012345678\n 9012\n@@ -29,32 +1,4 @@\n-9012345678901234567890123456\n 7890\n@@ -57,14 +1,3 @@\n-78901234567890\n+abc\n", dmp.patch_toText(patches));

            patches = dmp.patch_make("abcdefghij , h : 0 , t : 1 abcdefghij , h : 0 , t : 1 abcdefghij , h : 0 , t : 1", "abcdefghij , h : 1 , t : 1 abcdefghij , h : 1 , t : 1 abcdefghij , h : 0 , t : 1");
            dmp.patch_splitMax(patches);
            Assert.AreEqual("@@ -2,32 +2,32 @@\n bcdefghij , h : \n-0\n+1\n  , t : 1 abcdef\n@@ -29,32 +29,32 @@\n bcdefghij , h : \n-0\n+1\n  , t : 1 abcdef\n", dmp.patch_toText(patches));
        }

        [TestMethod]
        public void patch_addPaddingTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            List<Patch> patches;
            patches = dmp.patch_make("", "test");
            Assert.AreEqual("@@ -0,0 +1,4 @@\n+test\n",
               dmp.patch_toText(patches),
               "patch_addPadding: Both edges full.");
            dmp.patch_addPadding(patches);
            Assert.AreEqual("@@ -1,8 +1,12 @@\n %01%02%03%04\n+test\n %01%02%03%04\n",
                dmp.patch_toText(patches),
                "patch_addPadding: Both edges full.");

            patches = dmp.patch_make("XY", "XtestY");
            Assert.AreEqual("@@ -1,2 +1,6 @@\n X\n+test\n Y\n",
                dmp.patch_toText(patches),
                "patch_addPadding: Both edges partial.");
            dmp.patch_addPadding(patches);
            Assert.AreEqual("@@ -2,8 +2,12 @@\n %02%03%04X\n+test\n Y%01%02%03\n",
                dmp.patch_toText(patches),
                "patch_addPadding: Both edges partial.");

            patches = dmp.patch_make("XXXXYYYY", "XXXXtestYYYY");
            Assert.AreEqual("@@ -1,8 +1,12 @@\n XXXX\n+test\n YYYY\n",
                dmp.patch_toText(patches),
                "patch_addPadding: Both edges none.");
            dmp.patch_addPadding(patches);
            Assert.AreEqual("@@ -5,8 +5,12 @@\n XXXX\n+test\n YYYY\n",
               dmp.patch_toText(patches),
               "patch_addPadding: Both edges none.");
        }

        [TestMethod]
        public void patch_applyTest()
        {
            diff_match_patchTest dmp = new diff_match_patchTest();
            dmp.MatchDistance = 1000;
            dmp.MatchThreshold = 0.5f;
            dmp.PatchDeleteThreshold = 0.5f;
            List<Patch> patches;
            patches = dmp.patch_make("", "");
            Object[] results = dmp.patch_apply(patches, "Hello world.");
            bool[] boolArray = (bool[])results[1];
            string resultStr = results[0] + "\t" + boolArray.Length;
            Assert.AreEqual("Hello world.\t0", resultStr, "patch_apply: Null case.");

            patches = dmp.patch_make("The quick brown fox jumps over the lazy dog.", "That quick brown fox jumped over a lazy dog.");
            results = dmp.patch_apply(patches, "The quick brown fox jumps over the lazy dog.");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("That quick brown fox jumped over a lazy dog.\tTrue\tTrue", resultStr, "patch_apply: Exact match.");

            results = dmp.patch_apply(patches, "The quick red rabbit jumps over the tired tiger.");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("That quick red rabbit jumped over a tired tiger.\tTrue\tTrue", resultStr, "patch_apply: Partial match.");

            results = dmp.patch_apply(patches, "I am the very model of a modern major general.");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("I am the very model of a modern major general.\tFalse\tFalse", resultStr, "patch_apply: Failed match.");

            patches = dmp.patch_make("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            results = dmp.patch_apply(patches, "x123456789012345678901234567890-----++++++++++-----123456789012345678901234567890y");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("xabcy\tTrue\tTrue", resultStr, "patch_apply: Big delete, small change.");

            patches = dmp.patch_make("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            results = dmp.patch_apply(patches, "x12345678901234567890---------------++++++++++---------------12345678901234567890y");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("xabc12345678901234567890---------------++++++++++---------------12345678901234567890y\tFalse\tTrue", resultStr, "patch_apply: Big delete, big change 1.");

            dmp.PatchDeleteThreshold = 0.6f;
            patches = dmp.patch_make("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            results = dmp.patch_apply(patches, "x12345678901234567890---------------++++++++++---------------12345678901234567890y");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("xabcy\tTrue\tTrue", resultStr, "patch_apply: Big delete, big change 2.");
            dmp.PatchDeleteThreshold = 0.5f;

            dmp.MatchThreshold = 0.0f;
            dmp.MatchDistance = 0;
            patches = dmp.patch_make("abcdefghijklmnopqrstuvwxyz--------------------1234567890", "abcXXXXXXXXXXdefghijklmnopqrstuvwxyz--------------------1234567YYYYYYYYYY890");
            results = dmp.patch_apply(patches, "ABCDEFGHIJKLMNOPQRSTUVWXYZ--------------------1234567890");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ--------------------1234567YYYYYYYYYY890\tFalse\tTrue", resultStr, "patch_apply: Compensate for failed patch.");
            dmp.MatchThreshold = 0.5f;
            dmp.MatchDistance = 1000;

            patches = dmp.patch_make("", "test");
            string patchStr = dmp.patch_toText(patches);
            dmp.patch_apply(patches, "");
            Assert.AreEqual(patchStr, dmp.patch_toText(patches), "patch_apply: No side effects.");

            patches = dmp.patch_make("The quick brown fox jumps over the lazy dog.", "Woof");
            patchStr = dmp.patch_toText(patches);
            dmp.patch_apply(patches, "The quick brown fox jumps over the lazy dog.");
            Assert.AreEqual(patchStr, dmp.patch_toText(patches), "patch_apply: No side effects with major delete.");

            patches = dmp.patch_make("", "test");
            results = dmp.patch_apply(patches, "");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0];
            Assert.AreEqual("test\tTrue", resultStr, "patch_apply: Edge exact match.");

            patches = dmp.patch_make("XY", "XtestY");
            results = dmp.patch_apply(patches, "XY");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0];
            Assert.AreEqual("XtestY\tTrue", resultStr, "patch_apply: Near edge exact match.");

            patches = dmp.patch_make("y", "y123");
            results = dmp.patch_apply(patches, "x");
            boolArray = (bool[])results[1];
            resultStr = results[0] + "\t" + boolArray[0];
            Assert.AreEqual("x123\tTrue", resultStr, "patch_apply: Edge partial match.");
        }

        private static string[] diff_rebuildtexts(List<Diff> diffs)
        {
            string[] text = { "", "" };
            foreach (Diff myDiff in diffs)
            {
                if (myDiff.operation != Operation.INSERT)
                {
                    text[0] += myDiff.text;
                }
                if (myDiff.operation != Operation.DELETE)
                {
                    text[1] += myDiff.text;
                }
            }
            return text;
        }
    }
}
