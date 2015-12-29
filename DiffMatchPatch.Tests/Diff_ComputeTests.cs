using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class Diff_ComputeTests
    {
        [TestMethod]
        public void TrivialDiff()
        {
            var diffs = new List<Diff> { };
            CollectionAssert.AreEqual(diffs, Diff.Compute("", "", 1f, false), "diff_main: Null case.");
        }
        [TestMethod]
        public void Equality()
        {
            var expected1 = new List<Diff> { Diff.Equal("abc") };
            CollectionAssert.AreEqual(expected1, Diff.Compute("abc", "abc", 1f, false), "diff_main: Equality.");
        }
        [TestMethod]
        public void SimpleInsert()
        {
            var expected2 = new List<Diff> { Diff.Equal("ab"), Diff.Insert("123"), Diff.Equal("c") };
            CollectionAssert.AreEqual(expected2, Diff.Compute("abc", "ab123c", 1f, false), "diff_main: Simple insertion.");
        }

        [TestMethod]
        public void SimpleDelete()
        {
            var expected3 = new List<Diff> { Diff.Equal("a"), Diff.Delete("123"), Diff.Equal("bc") };
            CollectionAssert.AreEqual(expected3, Diff.Compute("a123bc", "abc", 1f, false), "diff_main: Simple deletion.");
        }

        [TestMethod]
        public void TwoInsertions()
        {
            var expected4 = new List<Diff>
            {
                Diff.Equal("a"),
                Diff.Insert("123"),
                Diff.Equal("b"),
                Diff.Insert("456"),
                Diff.Equal("c")
            };
            CollectionAssert.AreEqual(expected4, Diff.Compute("abc", "a123b456c", 1f, false), "diff_main: Two insertions.");

        }

        [TestMethod]
        public void TwoDeletes()
        {
            var expected5 = new List<Diff>
            {
                Diff.Equal("a"),
                Diff.Delete("123"),
                Diff.Equal("b"),
                Diff.Delete("456"),
                Diff.Equal("c")
            };
            CollectionAssert.AreEqual(expected5, Diff.Compute("a123b456c", "abc", 1f, false), "diff_main: Two deletions.");
        }

        [TestMethod]
        public void SimpleDeleteInsert_NoTimeout()
        {
            // Perform a real diff.
            // Switch off the timeout.
            var expected6 = new List<Diff> { Diff.Delete("a"), Diff.Insert("b") };
            CollectionAssert.AreEqual(expected6, Diff.Compute("a", "b", 0, false), "diff_main: Simple case #1.");
        }

        [TestMethod]
        public void SentenceChange1()
        {
            var expected7 = new List<Diff>
            {
                Diff.Delete("Apple"),
                Diff.Insert("Banana"),
                Diff.Equal("s are a"),
                Diff.Insert("lso"),
                Diff.Equal(" fruit.")
            };
            CollectionAssert.AreEqual(expected7, Diff.Compute("Apples are a fruit.", "Bananas are also fruit.", 0, false),
                "diff_main: Simple case #2.");
        }


        [TestMethod]
        public void SpecialCharacters_NoTimeout()
        {
            var expected8 = new List<Diff>
            {
                Diff.Delete("a"),
                Diff.Insert("\u0680"),
                Diff.Equal("x"),
                Diff.Delete("\t"),
                Diff.Insert(new string(new char[] {(char) 0}))
            };
            CollectionAssert.AreEqual(expected8, Diff.Compute("ax\t", "\u0680x" + (char)0, 0, false),
                "diff_main: Simple case #3.");
        }


        [TestMethod]
        public void DiffWithOverlap1()
        {
            var expected9 = new List<Diff>
            {
                Diff.Delete("1"),
                Diff.Equal("a"),
                Diff.Delete("y"),
                Diff.Equal("b"),
                Diff.Delete("2"),
                Diff.Insert("xab")
            };
            CollectionAssert.AreEqual(expected9, Diff.Compute("1ayb2", "abxab", 0, false), "diff_main: Overlap #1.");
        }


        [TestMethod]
        public void DiffWithOverlap2()
        {
            var expected10 = new List<Diff> { Diff.Insert("xaxcx"), Diff.Equal("abc"), Diff.Delete("y") };
            CollectionAssert.AreEqual(expected10, Diff.Compute("abcy", "xaxcxabc", 0, false), "diff_main: Overlap #2.");
        }

        [TestMethod]
        public void DiffWithOverlap3()
        {
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
                Diff.Compute("ABCDa=bcd=efghijklmnopqrsEFGHIJKLMNOefg", "a-bcd-efghijklmnopqrs", 0, false),
                "diff_main: Overlap #3.");
        }
        [TestMethod]
        public void LargeEquality()
        {
            var expected12 = new List<Diff>
            {
                Diff.Insert(" "),
                Diff.Equal("a"),
                Diff.Insert("nd"),
                Diff.Equal(" [[Pennsylvania]]"),
                Diff.Delete(" and [[New")
            };
            CollectionAssert.AreEqual(expected12,
                Diff.Compute("a [[Pennsylvania]] and [[New", " and [[Pennsylvania]]", 0, false),
                "diff_main: Large equality.");
        }

        [TestMethod]
        public void Timeout()
        {
            var timeoutInSeconds = 0.1f; // 100ms
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
            Diff.Compute(a, b, timeoutInSeconds);
            var endTime = DateTime.Now;
            // Test that we took at least the timeout period.
            Assert.IsTrue(new TimeSpan((long)(timeoutInSeconds * 1000) * 10000) <= endTime - startTime);
            // Test that we didn't take forever (be forgiving).
            // Theoretically this test could fail very occasionally if the
            // OS task swaps or locks up for a second at the wrong moment.
            Assert.IsTrue(new TimeSpan((long)(timeoutInSeconds * 1000) * 10000 * 2) > endTime - startTime);
        }

        [TestMethod]
        public void SimpleLinemodeSpeedup()
        {
            var timeoutInSeconds4 = 0;

            // Test the linemode speedup.
            // Must be long to pass the 100 char cutoff.
            var a =
                "1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n";
            var b =
                "abcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\nabcdefghij\n";
            CollectionAssert.AreEqual(
                Diff.Compute(a, b, timeoutInSeconds4, true), 
                Diff.Compute(a, b, timeoutInSeconds4, false),
                "diff_main: Simple line-mode.");
        }

        [TestMethod]
        public void SingleLineModeSpeedup()
        {
            var a = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
            var b = "abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij";
            CollectionAssert.AreEqual(Diff.Compute(a, b, 0, true), Diff.Compute(a, b, 0, false), "diff_main: Single line-mode.");
        }

        [TestMethod]
        public void OverlapLineMode()
        {
            var a = "1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n1234567890\n";
            var b = "abcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n1234567890\n1234567890\n1234567890\nabcdefghij\n";
            var textsLinemode = RebuildTexts(Diff.Compute(a, b, 0, true));
            var textsTextmode = RebuildTexts(Diff.Compute(a, b, 0, false));
            Assert.AreEqual(textsTextmode, textsLinemode, "diff_main: Overlap line-mode.");
        }

        private static Tuple<string, string> RebuildTexts(List<Diff> diffs)
        {
            var text = Tuple.Create(new StringBuilder(), new StringBuilder());
            foreach (var myDiff in diffs)
            {
                if (myDiff.Operation != Operation.Insert)
                {
                    text.Item1.Append(myDiff.Text);
                }
                if (myDiff.Operation != Operation.Delete)
                {
                    text.Item2.Append(myDiff.Text);
                }
            }
            return Tuple.Create(text.Item1.ToString(), text.Item2.ToString());
        }
    }
}