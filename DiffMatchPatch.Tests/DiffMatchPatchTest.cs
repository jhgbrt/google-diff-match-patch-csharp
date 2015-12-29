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

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class DiffMatchPatchTest 
    {
        [TestMethod]
        public void DiffPrettyHtmlTest()
        {
            
            // Pretty print.
            var diffs = new List<Diff>
            {
                Diff.Equal("a\n"),
                Diff.Delete("<B>b</B>"),
                Diff.Insert("c&d")
            };
            Assert.AreEqual(
                "<span>a&para;<br></span><del style=\"background:#ffe6e6;\">&lt;B&gt;b&lt;/B&gt;</del><ins style=\"background:#e6ffe6;\">c&amp;d</ins>",
                diffs.PrettyHtml());
        }

        [TestMethod]
        public void DiffTextTest()
        {
            
            // Compute the source and destination texts.
            var diffs = new List<Diff>
            {
                Diff.Equal("jump"),
                Diff.Delete("s"),
                Diff.Insert("ed"),
                Diff.Equal(" over "),
                Diff.Delete("the"),
                Diff.Insert("a"),
                Diff.Equal(" lazy")
            };
            Assert.AreEqual("jumps over the lazy", diffs.Text1());

            Assert.AreEqual("jumped over a lazy", diffs.Text2());
        }

        [TestMethod]
        public void DiffDeltaTest()
        {
            
            // Convert a diff into delta string.
            var diffs = new List<Diff>
            {
                Diff.Equal("jump"),
                Diff.Delete("s"),
                Diff.Insert("ed"),
                Diff.Equal(" over "),
                Diff.Delete("the"),
                Diff.Insert("a"),
                Diff.Equal(" lazy"),
                Diff.Insert("old dog")
            };
            var text1 = diffs.Text1();
            Assert.AreEqual("jumps over the lazy", text1);

            var delta = diffs.ToDelta();
            Assert.AreEqual("=4\t-1\t+ed\t=6\t-3\t+a\t=5\t+old dog", delta);

            // Convert delta string into a diff.
            CollectionAssert.AreEqual(diffs, DiffList.FromDelta(text1, delta));

            // Generates error (19 < 20).
            try
            {
                DiffList.FromDelta(text1 + "x", delta);
                Assert.Fail("diff_fromDelta: Too long.");
            }
            catch (ArgumentException)
            {
                // Exception expected.
            }

            // Generates error (19 > 18).
            try
            {
                DiffList.FromDelta(text1.Substring(1), delta);
                Assert.Fail("diff_fromDelta: Too short.");
            }
            catch (ArgumentException)
            {
                // Exception expected.
            }

            // Generates error (%c3%xy invalid Unicode).
            try
            {
                DiffList.FromDelta("", "+%c3%xy");
                Assert.Fail("diff_fromDelta: Invalid character.");
            }
            catch (ArgumentException)
            {
                // Exception expected.
            }

            // Test deltas with special characters.
            var zero = (char) 0;
            var one = (char) 1;
            var two = (char) 2;
            diffs = new List<Diff>
            {
                Diff.Equal("\u0680 " + zero + " \t %"),
                Diff.Delete("\u0681 " + one + " \n ^"),
                Diff.Insert("\u0682 " + two + " \\ |")
            };
            text1 = diffs.Text1();
            Assert.AreEqual("\u0680 " + zero + " \t %\u0681 " + one + " \n ^", text1);

            delta = diffs.ToDelta();
            // Lowercase, due to UrlEncode uses lower.
            Assert.AreEqual("=7\t-7\t+%da%82 %02 %5c %7c", delta, "diff_toDelta: Unicode.");

            CollectionAssert.AreEqual(diffs, DiffList.FromDelta(text1, delta), "diff_fromDelta: Unicode.");

            // Verify pool of unchanged characters.
            diffs = new List<Diff>
            {
                Diff.Insert("A-Z a-z 0-9 - _ . ! ~ * ' ( ) ; / ? : @ & = + $ , # ")
            };
            var text2 = diffs.Text2();
            Assert.AreEqual("A-Z a-z 0-9 - _ . ! ~ * \' ( ) ; / ? : @ & = + $ , # ", text2,
                "diff_text2: Unchanged characters.");

            delta = diffs.ToDelta();
            Assert.AreEqual("+A-Z a-z 0-9 - _ . ! ~ * \' ( ) ; / ? : @ & = + $ , # ", delta,
                "diff_toDelta: Unchanged characters.");

            // Convert delta string into a diff.
            CollectionAssert.AreEqual(diffs, DiffList.FromDelta("", delta), "diff_fromDelta: Unchanged characters.");
        }

        [TestMethod]
        public void DiffXIndexTest()
        {
            
            // Translate a location in text1 to text2.
            var diffs = new List<Diff>
            {
                Diff.Delete("a"),
                Diff.Insert("1234"),
                Diff.Equal("xyz")
            };
            Assert.AreEqual(5, diffs.FindEquivalentLocation2(2), "diff_xIndex: Translation on equality.");

            diffs = new List<Diff>
            {
                Diff.Equal("a"),
                Diff.Delete("1234"),
                Diff.Equal("xyz")
            };
            Assert.AreEqual(1, diffs.FindEquivalentLocation2(3), "diff_xIndex: Translation on deletion.");
        }

        [TestMethod]
        public void DiffLevenshteinTest()
        {
            
            var diffs = new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Insert("1234"),
                Diff.Equal("xyz")
            };
            Assert.AreEqual(4, diffs.Levenshtein(), "diff_levenshtein: Levenshtein with trailing equality.");

            diffs = new List<Diff>
            {
                Diff.Equal("xyz"),
                Diff.Delete("abc"),
                Diff.Insert("1234")
            };
            Assert.AreEqual(4, diffs.Levenshtein(), "diff_levenshtein: Levenshtein with leading equality.");

            diffs = new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Equal("xyz"),
                Diff.Insert("1234")
            };
            Assert.AreEqual(7, diffs.Levenshtein(), "diff_levenshtein: Levenshtein with middle equality.");
        }

        [TestMethod]
        public void DiffBisectTest()
        {
            
            // Normal.
            var a = "cat";
            var b = "map";
            // Since the resulting diff hasn't been normalized, it would be ok if
            // the insertion and deletion pairs are swapped.
            // If the order changes, tweak this test as required.
            var diffs = new List<Diff>
            {
                Diff.Delete("c"),
                Diff.Insert("m"),
                Diff.Equal("a"),
                Diff.Delete("t"),
                Diff.Insert("p")
            };
            CollectionAssert.AreEqual(diffs, Diff.MyersDiffBisect(a, b, new CancellationToken(), false));

            // Timeout.
            diffs = new List<Diff> {Diff.Delete("cat"), Diff.Insert("map")};
            CollectionAssert.AreEqual(diffs, Diff.MyersDiffBisect(a, b, new CancellationToken(true), true));
        }

        [TestMethod]
        public void DiffMainTest()
        {
            // Perform a trivial diff.
            var diffs = new List<Diff> {};
            CollectionAssert.AreEqual(diffs, Diff.Compute("", "", 1f, false), "diff_main: Null case.");

            var expected1 = new List<Diff> {Diff.Equal("abc")};
            CollectionAssert.AreEqual(expected1, Diff.Compute("abc", "abc", 1f, false), "diff_main: Equality.");

            var expected2 = new List<Diff> { Diff.Equal("ab"), Diff.Insert("123"), Diff.Equal("c") };
            CollectionAssert.AreEqual(expected2, Diff.Compute("abc", "ab123c", 1f, false), "diff_main: Simple insertion.");

            var expected3 = new List<Diff> { Diff.Equal("a"), Diff.Delete("123"), Diff.Equal("bc") };
            CollectionAssert.AreEqual(expected3, Diff.Compute("a123bc", "abc", 1f, false), "diff_main: Simple deletion.");

            var expected4 = new List<Diff>
            {
                Diff.Equal("a"),
                Diff.Insert("123"),
                Diff.Equal("b"),
                Diff.Insert("456"),
                Diff.Equal("c")
            };
            CollectionAssert.AreEqual(expected4, Diff.Compute("abc", "a123b456c", 1f, false), "diff_main: Two insertions.");

            var expected5 = new List<Diff>
            {
                Diff.Equal("a"),
                Diff.Delete("123"),
                Diff.Equal("b"),
                Diff.Delete("456"),
                Diff.Equal("c")
            };
            CollectionAssert.AreEqual(expected5, Diff.Compute("a123b456c", "abc", 1f, false), "diff_main: Two deletions.");

            // Perform a real diff.
            // Switch off the timeout.
            var timeoutInSeconds2 = 0;
            var expected6 = new List<Diff> { Diff.Delete("a"), Diff.Insert("b") };
            CollectionAssert.AreEqual(expected6, Diff.Compute("a", "b", timeoutInSeconds2, false), "diff_main: Simple case #1.");

            var expected7 = new List<Diff>
            {
                Diff.Delete("Apple"),
                Diff.Insert("Banana"),
                Diff.Equal("s are a"),
                Diff.Insert("lso"),
                Diff.Equal(" fruit.")
            };
            CollectionAssert.AreEqual(expected7, Diff.Compute("Apples are a fruit.", "Bananas are also fruit.", timeoutInSeconds2, false),
                "diff_main: Simple case #2.");

            var expected8 = new List<Diff>
            {
                Diff.Delete("a"),
                Diff.Insert("\u0680"),
                Diff.Equal("x"),
                Diff.Delete("\t"),
                Diff.Insert(new string(new char[] {(char) 0}))
            };
            CollectionAssert.AreEqual(expected8, Diff.Compute("ax\t", "\u0680x" + (char) 0, timeoutInSeconds2, false),
                "diff_main: Simple case #3.");

            var expected9 = new List<Diff>
            {
                Diff.Delete("1"),
                Diff.Equal("a"),
                Diff.Delete("y"),
                Diff.Equal("b"),
                Diff.Delete("2"),
                Diff.Insert("xab")
            };
            CollectionAssert.AreEqual(expected9, Diff.Compute("1ayb2", "abxab", timeoutInSeconds2, false), "diff_main: Overlap #1.");

            var expected10 = new List<Diff> { Diff.Insert("xaxcx"), Diff.Equal("abc"), Diff.Delete("y") };
            CollectionAssert.AreEqual(expected10, Diff.Compute("abcy", "xaxcxabc", timeoutInSeconds2, false), "diff_main: Overlap #2.");

            var expected11 = new List<Diff>
            {
                Diff.Delete("ABCD"),
                Diff.Equal("a"),
                Diff.Delete("="),
                Diff.Insert("-"),
                Diff.Equal("bcd"),
                Diff.Delete("="),
                Diff.Insert("-"),
                Diff.Equal("efghijklmnopqrs"),
                Diff.Delete("EFGHIJKLMNOefg")
            };
            CollectionAssert.AreEqual(expected11,
                Diff.Compute("ABCDa=bcd=efghijklmnopqrsEFGHIJKLMNOefg", "a-bcd-efghijklmnopqrs", timeoutInSeconds2, false),
                "diff_main: Overlap #3.");

            var expected12 = new List<Diff>
            {
                Diff.Insert(" "),
                Diff.Equal("a"),
                Diff.Insert("nd"),
                Diff.Equal(" [[Pennsylvania]]"),
                Diff.Delete(" and [[New")
            };
            CollectionAssert.AreEqual(expected12,
                Diff.Compute("a [[Pennsylvania]] and [[New", " and [[Pennsylvania]]", timeoutInSeconds2, false),
                "diff_main: Large equality.");

            var timeoutInSeconds3 = 0.1f; // 100ms
            var a =
                "`Twas brillig, and the slithy toves\nDid gyre and gimble in the wabe:\nAll mimsy were the borogoves,\nAnd the mome raths outgrabe.\n";
            var b =
                "I am the very model of a modern major general,\nI've information vegetable, animal, and mineral,\nI know the kings of England, and I quote the fights historical,\nFrom Marathon to Waterloo, in order categorical.\n";
            // Increase the text lengths by 1024 times to ensure a timeout.
            for (var x = 0; x < 10; x++)
            {
                a = a + a;
                b = b + b;
            }
            var startTime = DateTime.Now;
            Diff.Compute(a, b, timeoutInSeconds3);
            var endTime = DateTime.Now;
            // Test that we took at least the timeout period.
            Assert.IsTrue(new TimeSpan((long)(timeoutInSeconds3 * 1000) * 10000) <= endTime - startTime);
            // Test that we didn't take forever (be forgiving).
            // Theoretically this test could fail very occasionally if the
            // OS task swaps or locks up for a second at the wrong moment.
            Assert.IsTrue(new TimeSpan((long)(timeoutInSeconds3 * 1000) * 10000 * 2) > endTime - startTime);
            var timeoutInSeconds4 = 0;

            // Test the linemode speedup.
            // Must be long to pass the 100 char cutoff.
            a =
                "1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n";
            b =
                "abcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\n";
            CollectionAssert.AreEqual(Diff.Compute(a, b, timeoutInSeconds4, true), Diff.Compute(a, b, timeoutInSeconds4, false),
                "diff_main: Simple line-mode.");

            a =
                "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
            b =
                "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij";
            CollectionAssert.AreEqual(Diff.Compute(a, b, timeoutInSeconds4, true), Diff.Compute(a, b, timeoutInSeconds4, false),
                "diff_main: Single line-mode.");

            a =
                "1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n";
            b =
                "abcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n";
            var textsLinemode = DiffRebuildtexts(Diff.Compute(a, b, timeoutInSeconds4));
            var textsTextmode = DiffRebuildtexts(Diff.Compute(a, b, timeoutInSeconds4, false));
            CollectionAssert.AreEqual(textsTextmode, textsLinemode, "diff_main: Overlap line-mode.");
            // Test null inputs -- not needed because nulls can't be passed in C#.
        }

        [TestMethod]
        public void MatchAlphabetTest()
        {
            // Initialise the bitmasks for Bitap.
            var bitmask = new Dictionary<char, int>();
            bitmask.Add('a', 4);
            bitmask.Add('b', 2);
            bitmask.Add('c', 1);
            CollectionAssert.AreEqual(bitmask, BitapAlgorithm.InitAlphabet("abc"), "match_alphabet: Unique.");

            bitmask.Clear();
            bitmask.Add('a', 37);
            bitmask.Add('b', 18);
            bitmask.Add('c', 8);
            CollectionAssert.AreEqual(bitmask, BitapAlgorithm.InitAlphabet("abcaba"), "match_alphabet: Duplicates.");
        }

        [TestMethod]
        public void MatchBitapTest()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));

            Assert.AreEqual(5, dmp.Match("abcdefghijk", "fgh", 5), "match_bitap: Exact match #1.");

            Assert.AreEqual(5, dmp.Match("abcdefghijk", "fgh", 0), "match_bitap: Exact match #2.");

            Assert.AreEqual(4, dmp.Match("abcdefghijk", "efxhi", 0), "match_bitap: Fuzzy match #1.");

            Assert.AreEqual(2, dmp.Match("abcdefghijk", "cdefxyhijk", 5), "match_bitap: Fuzzy match #2.");

            Assert.AreEqual(-1, dmp.Match("abcdefghijk", "bxy", 1), "match_bitap: Fuzzy match #3.");

            Assert.AreEqual(2, dmp.Match("123456789xx0", "3456789x0", 2), "match_bitap: Overflow.");

            Assert.AreEqual(0, dmp.Match("abcdef", "xxabc", 4), "match_bitap: Before start match.");

            Assert.AreEqual(3, dmp.Match("abcdef", "defyy", 4), "match_bitap: Beyond end match.");

            Assert.AreEqual(0, dmp.Match("abcdef", "xabcdefy", 0), "match_bitap: Oversized pattern.");

            dmp = new BitapAlgorithm(new MatchSettings(0.4f, 100));
            Assert.AreEqual(4, dmp.Match("abcdefghijk", "efxyhi", 1), "match_bitap: Threshold #1.");

            dmp = new BitapAlgorithm(new MatchSettings(0.3f, 100));
            Assert.AreEqual(-1, dmp.Match("abcdefghijk", "efxyhi", 1), "match_bitap: Threshold #2.");

            dmp = new BitapAlgorithm(new MatchSettings(0.0f, 100));
            Assert.AreEqual(1, dmp.Match("abcdefghijk", "bcdef", 1), "match_bitap: Threshold #3.");

            dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(0, dmp.Match("abcdexyzabcde", "abccde", 3), "match_bitap: Multiple select #1.");

            Assert.AreEqual(8, dmp.Match("abcdexyzabcde", "abccde", 5), "match_bitap: Multiple select #2.");

            dmp = new BitapAlgorithm(new MatchSettings(0.5f, 10));
            Assert.AreEqual(-1, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdefg", 24),
                "match_bitap: Distance test #1.");

            Assert.AreEqual(0, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdxxefg", 1),
                "match_bitap: Distance test #2.");

            dmp = new BitapAlgorithm(new MatchSettings(0.5f, 1000));
            Assert.AreEqual(0, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdefg", 24),
                "match_bitap: Distance test #3.");
        }

        [TestMethod]
        public void MatchMainTest()
        {
            
            // Full match.
            Assert.AreEqual(0, "abcdef".MatchPattern("abcdef", 1000), "match_main: Equality.");

            Assert.AreEqual(-1, "".MatchPattern("abcdef", 1), "match_main: Null text.");

            Assert.AreEqual(3, "abcdef".MatchPattern("", 3), "match_main: Null pattern.");

            Assert.AreEqual(3, "abcdef".MatchPattern("de", 3), "match_main: Exact match.");

            Assert.AreEqual(3, "abcdef".MatchPattern("defy", 4), "match_main: Beyond end match.");

            Assert.AreEqual(0, "abcdef".MatchPattern("abcdefy", 0), "match_main: Oversized pattern.");

            Assert.AreEqual(4, "I am the very model of a modern major general.".MatchPattern(" that berry ", 5, new MatchSettings(0.7f, 1000)),
                "match_main: Complex match.");

            // Test null inputs -- not needed because nulls can't be passed in C#.
        }

        [TestMethod]
        public void PatchPatchObjTest()
        {
            // Patch Object.
            var p = new Patch
            {
                Start1 = 20,
                Start2 = 21,
                Length1 = 18,
                Length2 = 17
            };
            p.Diffs.AddRange(new[]
            {
                Diff.Equal("jump"),
                Diff.Delete("s"),
                Diff.Insert("ed"),
                Diff.Equal(" over "),
                Diff.Delete("the"),
                Diff.Insert("a"),
                Diff.Equal("\nlaz")
            });
            var strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n %0alaz\n";
            Assert.AreEqual(strp, p.ToString(), "Patch: toString.");
        }

        [TestMethod]
        public void PatchFromTextTest()
        {
            
            Assert.IsTrue(PatchList.Parse("").Count == 0, "patch_fromText: #0.");

            var strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n %0alaz\n";
            Assert.AreEqual(strp, PatchList.Parse(strp)[0].ToString(), "patch_fromText: #1.");

            Assert.AreEqual("@@ -1 +1 @@\n-a\n+b\n", PatchList.Parse("@@ -1 +1 @@\n-a\n+b\n")[0].ToString(),
                "patch_fromText: #2.");

            Assert.AreEqual("@@ -1,3 +0,0 @@\n-abc\n", PatchList.Parse("@@ -1,3 +0,0 @@\n-abc\n")[0].ToString(),
                "patch_fromText: #3.");

            Assert.AreEqual("@@ -0,0 +1,3 @@\n+abc\n", PatchList.Parse("@@ -0,0 +1,3 @@\n+abc\n")[0].ToString(),
                "patch_fromText: #4.");

            // Generates error.
            try
            {
                PatchList.Parse("Bad\nPatch\n");
                Assert.Fail("patch_fromText: #5.");
            }
            catch (ArgumentException)
            {
                // Exception expected.
            }
        }

        [TestMethod]
        public void PatchToTextTest()
        {
            
            var strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n  laz\n";
            List<Patch> patches;
            patches = PatchList.Parse(strp);
            var result = patches.ToText();
            Assert.AreEqual(strp, result);

            strp = "@@ -1,9 +1,9 @@\n-f\n+F\n oo+fooba\n@@ -7,9 +7,9 @@\n obar\n-,\n+.\n  tes\n";
            patches = PatchList.Parse(strp);
            result = patches.ToText();
            Assert.AreEqual(strp, result);
        }

        [TestMethod]
        public void PatchAddContextTest()
        {
            
            Patch p;
            p = PatchList.Parse("@@ -21,4 +21,10 @@\n-jump\n+somersault\n")[0];
            p.AddContext("The quick brown fox jumps over the lazy dog.");
            Assert.AreEqual("@@ -17,12 +17,18 @@\n fox \n-jump\n+somersault\n s ov\n", p.ToString(),
                "patch_addContext: Simple case.");

            p = PatchList.Parse("@@ -21,4 +21,10 @@\n-jump\n+somersault\n")[0];
            p.AddContext("The quick brown fox jumps.");
            Assert.AreEqual("@@ -17,10 +17,16 @@\n fox \n-jump\n+somersault\n s.\n", p.ToString(),
                "patch_addContext: Not enough trailing context.");

            p = PatchList.Parse("@@ -3 +3,2 @@\n-e\n+at\n")[0];
            p.AddContext("The quick brown fox jumps.");
            Assert.AreEqual("@@ -1,7 +1,8 @@\n Th\n-e\n+at\n  qui\n", p.ToString(),
                "patch_addContext: Not enough leading context.");

            p = PatchList.Parse("@@ -3 +3,2 @@\n-e\n+at\n")[0];
            p.AddContext("The quick brown fox jumps.  The quick brown fox crashes.");
            Assert.AreEqual("@@ -1,27 +1,28 @@\n Th\n-e\n+at\n  quick brown fox jumps. \n", p.ToString(),
                "patch_addContext: Ambiguity.");
        }

        [TestMethod]
        public void PatchMakeTest()
        {
            
            List<Patch> patches;
            patches = Patch.Compute("", "");
            Assert.AreEqual("", patches.ToText(), "patch_make: Null case.");

            var text1 = "The quick brown fox jumps over the lazy dog.";
            var text2 = "That quick brown fox jumped over a lazy dog.";
            var expectedPatch =
                "@@ -1,8 +1,7 @@\n Th\n-at\n+e\n  qui\n@@ -21,17 +21,18 @@\n jump\n-ed\n+s\n  over \n-a\n+the\n  laz\n";
            // The second patch must be "-21,17 +21,18", not "-22,17 +21,18" due to rolling context.
            patches = Patch.Compute(text2, text1);
            Assert.AreEqual(expectedPatch, patches.ToText(), "patch_make: Text2+Text1 inputs.");

            expectedPatch =
                "@@ -1,11 +1,12 @@\n Th\n-e\n+at\n  quick b\n@@ -22,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n  laz\n";
            patches = Patch.Compute(text1, text2);
            Assert.AreEqual(expectedPatch, patches.ToText(), "patch_make: Text1+Text2 inputs.");

            var diffs = Diff.Compute(text1, text2, 0, false);
            patches = Patch.Compute(diffs);
            Assert.AreEqual(expectedPatch, patches.ToText(), "patch_make: Diff input.");

            patches = Patch.Compute(text1, diffs);
            Assert.AreEqual(expectedPatch, patches.ToText(), "patch_make: Text1+Diff inputs.");

            patches = Patch.Compute(text1, diffs);
            Assert.AreEqual(expectedPatch, patches.ToText(),
                "patch_make: Text1+Text2+Diff inputs (deprecated).");

            patches = Patch.Compute("`1234567890-=[]\\;',./", "~!@#$%^&*()_+{}|:\"<>?");
            Assert.AreEqual(
                "@@ -1,21 +1,21 @@\n-%601234567890-=%5b%5d%5c;',./\n+~!@#$%25%5e&*()_+%7b%7d%7c:%22%3c%3e?\n",
                patches.ToText(),
                "patch_toText: Character encoding.");

            diffs = new List<Diff>
            {
                Diff.Delete("`1234567890-=[]\\;',./"),
                Diff.Insert("~!@#$%^&*()_+{}|:\"<>?")
            };
            CollectionAssert.AreEqual(diffs,
                PatchList.Parse("@@ -1,21 +1,21 @@\n-%601234567890-=%5B%5D%5C;',./\n+~!@#$%25%5E&*()_+%7B%7D%7C:%22%3C%3E?\n")[0]
                    .Diffs,
                "patch_fromText: Character decoding.");

            text1 = "";
            for (var x = 0; x < 100; x++)
            {
                text1 += "abcdef";
            }
            text2 = text1 + "123";
            expectedPatch = "@@ -573,28 +573,31 @@\n cdefabcdefabcdefabcdefabcdef\n+123\n";
            patches = Patch.Compute(text1, text2);
            Assert.AreEqual(expectedPatch, patches.ToText(), "patch_make: Long string with repeats.");

            // Test null inputs -- not needed because nulls can't be passed in C#.
        }

        [TestMethod]
        public void PatchSplitMaxTest()
        {
            var patches = Patch.Compute("abcdefghijklmnopqrstuvwxyz01234567890", "XabXcdXefXghXijXklXmnXopXqrXstXuvXwxXyzX01X23X45X67X89X0");
            patches.SplitMax();
            Assert.AreEqual(
                "@@ -1,32 +1,46 @@\n+X\n ab\n+X\n cd\n+X\n ef\n+X\n gh\n+X\n ij\n+X\n kl\n+X\n mn\n+X\n op\n+X\n qr\n+X\n st\n+X\n uv\n+X\n wx\n+X\n yz\n+X\n 012345\n@@ -25,13 +39,18 @@\n zX01\n+X\n 23\n+X\n 45\n+X\n 67\n+X\n 89\n+X\n 0\n",
                patches.ToText());

            patches = Patch.Compute("abcdef1234567890123456789012345678901234567890123456789012345678901234567890uvwxyz", "abcdefuvwxyz");
            var oldToText = patches.ToText();
            patches.SplitMax();
            Assert.AreEqual(oldToText, patches.ToText());

            patches = Patch.Compute("1234567890123456789012345678901234567890123456789012345678901234567890", "abc");
            patches.SplitMax();
            Assert.AreEqual(
                "@@ -1,32 +1,4 @@\n-1234567890123456789012345678\n 9012\n@@ -29,32 +1,4 @@\n-9012345678901234567890123456\n 7890\n@@ -57,14 +1,3 @@\n-78901234567890\n+abc\n",
                patches.ToText());

            patches = Patch.Compute("abcdefghij , h : 0 , t : 1 abcdefghij , h : 0 , t : 1 abcdefghij , h : 0 , t : 1", "abcdefghij , h : 1 , t : 1 abcdefghij , h : 1 , t : 1 abcdefghij , h : 0 , t : 1");
            patches.SplitMax();
            Assert.AreEqual(
                "@@ -2,32 +2,32 @@\n bcdefghij , h : \n-0\n+1\n  , t : 1 abcdef\n@@ -29,32 +29,32 @@\n bcdefghij , h : \n-0\n+1\n  , t : 1 abcdef\n",
                patches.ToText());
        }

        [TestMethod]
        public void PatchAddPaddingTest()
        {
            List<Patch> patches;
            patches = Patch.Compute("", "test");
            Assert.AreEqual("@@ -0,0 +1,4 @@\n+test\n",
                patches.ToText(),
                "patch_addPadding: Both edges full.");
            patches.AddPadding();
            Assert.AreEqual("@@ -1,8 +1,12 @@\n %01%02%03%04\n+test\n %01%02%03%04\n",
                patches.ToText(),
                "patch_addPadding: Both edges full.");

            patches = Patch.Compute("XY", "XtestY");
            Assert.AreEqual("@@ -1,2 +1,6 @@\n X\n+test\n Y\n",
                patches.ToText(),
                "patch_addPadding: Both edges partial.");
            patches.AddPadding();
            Assert.AreEqual("@@ -2,8 +2,12 @@\n %02%03%04X\n+test\n Y%01%02%03\n",
                patches.ToText(),
                "patch_addPadding: Both edges partial.");

            patches = Patch.Compute("XXXXYYYY", "XXXXtestYYYY");
            Assert.AreEqual("@@ -1,8 +1,12 @@\n XXXX\n+test\n YYYY\n",
                patches.ToText(),
                "patch_addPadding: Both edges none.");
            patches.AddPadding();
            Assert.AreEqual("@@ -5,8 +5,12 @@\n XXXX\n+test\n YYYY\n",
                patches.ToText(),
                "patch_addPadding: Both edges none.");
        }

        [TestMethod]
        public void PatchApplyTest()
        {
            
            List<Patch> patches;
            patches = Patch.Compute("", "");
            var results = patches.Apply("Hello world.");
            var boolArray = results.Item2;
            var resultStr = results.Item1 + "\t" + boolArray.Length;
            Assert.AreEqual("Hello world.\t0", resultStr, "patch_apply: Null case.");

            patches = Patch.Compute("The quick brown fox jumps over the lazy dog.", "That quick brown fox jumped over a lazy dog.");
            results = patches.Apply("The quick brown fox jumps over the lazy dog.");
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("That quick brown fox jumped over a lazy dog.\tTrue\tTrue", resultStr,
                "patch_apply: Exact match.");

            results = patches.Apply("The quick red rabbit jumps over the tired tiger.");
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("That quick red rabbit jumped over a tired tiger.\tTrue\tTrue", resultStr,
                "patch_apply: Partial match.");

            results = patches.Apply("I am the very model of a modern major general.");
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("I am the very model of a modern major general.\tFalse\tFalse", resultStr,
                "patch_apply: Failed match.");

            patches = Patch.Compute("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            results = patches.Apply("x123456789012345678901234567890-----++++++++++-----123456789012345678901234567890y");
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("xabcy\tTrue\tTrue", resultStr, "patch_apply: Big delete, small change.");

            patches = Patch.Compute("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            results = patches.Apply("x12345678901234567890---------------++++++++++---------------12345678901234567890y");
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual(
                "xabc12345678901234567890---------------++++++++++---------------12345678901234567890y\tFalse\tTrue",
                resultStr, "patch_apply: Big delete, big change 1.");

            patches = Patch.Compute("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            results = patches.Apply("x12345678901234567890---------------++++++++++---------------12345678901234567890y", MatchSettings.Default, new PatchSettings(0.6f, 4));
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("xabcy\tTrue\tTrue", resultStr, "patch_apply: Big delete, big change 2.");

            patches = Patch.Compute("abcdefghijklmnopqrstuvwxyz--------------------1234567890", "abcXXXXXXXXXXdefghijklmnopqrstuvwxyz--------------------1234567YYYYYYYYYY890");
            results = patches.Apply("ABCDEFGHIJKLMNOPQRSTUVWXYZ--------------------1234567890", new MatchSettings(0.0f,0));
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.AreEqual("ABCDEFGHIJKLMNOPQRSTUVWXYZ--------------------1234567YYYYYYYYYY890\tFalse\tTrue", resultStr,
                "patch_apply: Compensate for failed patch.");

            patches = Patch.Compute("", "test");
            var patchStr = patches.ToText();
            patches.Apply("");
            Assert.AreEqual(patchStr, patches.ToText(), "patch_apply: No side effects.");

            patches = Patch.Compute("The quick brown fox jumps over the lazy dog.", "Woof");
            patchStr = patches.ToText();
            patches.Apply("The quick brown fox jumps over the lazy dog.");
            Assert.AreEqual(patchStr, patches.ToText(), "patch_apply: No side effects with major delete.");

            patches = Patch.Compute("", "test");
            results = patches.Apply("");
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0];
            Assert.AreEqual("test\tTrue", resultStr, "patch_apply: Edge exact match.");

            patches = Patch.Compute("XY", "XtestY");
            results = patches.Apply("XY");
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0];
            Assert.AreEqual("XtestY\tTrue", resultStr, "patch_apply: Near edge exact match.");

            patches = Patch.Compute("y", "y123");
            results = patches.Apply("x");
            boolArray = results.Item2;
            resultStr = results.Item1 + "\t" + boolArray[0];
            Assert.AreEqual("x123\tTrue", resultStr, "patch_apply: Edge partial match.");
        }

        private static string[] DiffRebuildtexts(List<Diff> diffs)
        {
            string[] text = {"", ""};
            foreach (var myDiff in diffs)
            {
                if (myDiff.Operation != Operation.Insert)
                {
                    text[0] += myDiff.Text;
                }
                if (myDiff.Operation != Operation.Delete)
                {
                    text[1] += myDiff.Text;
                }
            }
            return text;
        }
    }
}
