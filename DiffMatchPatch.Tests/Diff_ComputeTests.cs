using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static DiffMatchPatch.Diff;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class Diff_ComputeTests
    {
        [TestMethod]
        public void TrivialDiff()
        {
            var diffs = new List<Diff>();
            CollectionAssert.AreEqual(diffs, Diff.Compute("", "", 1f, false), "diff_main: Null case.");
        }
        [TestMethod]
        public void Equality()
        {
            var expected1 = new List<Diff> { Equal("abc") };
            CollectionAssert.AreEqual(expected1, Diff.Compute("abc", "abc", 1f, false), "diff_main: Equality.");
        }
        [TestMethod]
        public void SimpleInsert()
        {
            var expected2 = new List<Diff> { Equal("ab"), Insert("123"), Equal("c") };
            CollectionAssert.AreEqual(expected2, Diff.Compute("abc", "ab123c", 1f, false), "diff_main: Simple insertion.");
        }

        [TestMethod]
        public void SimpleDelete()
        {
            var expected3 = new List<Diff> { Equal("a"), Delete("123"), Equal("bc") };
            CollectionAssert.AreEqual(expected3, Diff.Compute("a123bc", "abc", 1f, false), "diff_main: Simple deletion.");
        }

        [TestMethod]
        public void TwoInsertions()
        {
            var expected4 = new List<Diff>
            {
                Equal("a"),
                Insert("123"),
                Equal("b"),
                Insert("456"),
                Equal("c")
            };
            CollectionAssert.AreEqual(expected4, Diff.Compute("abc", "a123b456c", 1f, false), "diff_main: Two insertions.");

        }

        [TestMethod]
        public void TwoDeletes()
        {
            var expected5 = new List<Diff>
            {
                Equal("a"),
                Delete("123"),
                Equal("b"),
                Delete("456"),
                Equal("c")
            };
            CollectionAssert.AreEqual(expected5, Diff.Compute("a123b456c", "abc", 1f, false), "diff_main: Two deletions.");
        }

        [TestMethod]
        public void SimpleDeleteInsert_NoTimeout()
        {
            // Perform a real diff.
            // Switch off the timeout.
            var expected6 = new List<Diff> { Delete("a"), Insert("b") };
            CollectionAssert.AreEqual(expected6, Diff.Compute("a", "b", 0, false), "diff_main: Simple case #1.");
        }

        [TestMethod]
        public void SentenceChange1()
        {
            var expected7 = new List<Diff>
            {
                Delete("Apple"),
                Insert("Banana"),
                Equal("s are a"),
                Insert("lso"),
                Equal(" fruit.")
            };
            CollectionAssert.AreEqual(expected7, Diff.Compute("Apples are a fruit.", "Bananas are also fruit.", 0, false),
                "diff_main: Simple case #2.");
        }


        [TestMethod]
        public void SpecialCharacters_NoTimeout()
        {
            var expected8 = new List<Diff>
            {
                Delete("a"),
                Insert("\u0680"),
                Equal("x"),
                Delete("\t"),
                Insert(new string(new char[] {(char) 0}))
            };
            CollectionAssert.AreEqual(expected8, Diff.Compute("ax\t", "\u0680x" + (char)0, 0, false),
                "diff_main: Simple case #3.");
        }


        [TestMethod]
        public void DiffWithOverlap1()
        {
            var expected9 = new List<Diff>
            {
                Delete("1"),
                Equal("a"),
                Delete("y"),
                Equal("b"),
                Delete("2"),
                Insert("xab")
            };
            CollectionAssert.AreEqual(expected9, Diff.Compute("1ayb2", "abxab", 0, false), "diff_main: Overlap #1.");
        }


        [TestMethod]
        public void DiffWithOverlap2()
        {
            var expected10 = new List<Diff> { Insert("xaxcx"), Equal("abc"), Delete("y") };
            CollectionAssert.AreEqual(expected10, Diff.Compute("abcy", "xaxcxabc", 0, false), "diff_main: Overlap #2.");
        }

        [TestMethod]
        public void DiffWithOverlap3()
        {
            var expected11 = new List<Diff>
            {
                Delete("ABCD"),
                Equal("a"),
                Delete("="),
                Insert("-"),
                Equal("bcd"),
                Delete("="),
                Insert("-"),
                Equal("efghijklmnopqrs"),
                Delete("EFGHIJKLMNOefg")
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
                Insert(" "),
                Equal("a"),
                Insert("nd"),
                Equal(" [[Pennsylvania]]"),
                Delete(" and [[New")
            };
            CollectionAssert.AreEqual(expected12,
                Diff.Compute("a [[Pennsylvania]] and [[New", " and [[Pennsylvania]]", 0, false),
                "diff_main: Large equality.");
        }

        [TestMethod]
        public void Compute_WithHalfMatch()
        {
            var a = "Lorem ipsum dolor sit amet, consectetuer adipiscing elit, \r\nsed diam nonummy nibh euismod tincidunt ut laoreet dolore magna \r\naliquam erat volutpat. Ut wisi enim ad minim veniam, quis nostrud exerci \r\ntation ullamcorper suscipit lobortis nisl ut aliquip ex ea commodo consequat. \r\nDuis autem vel eum iriure dolor in hendrerit in vulputate velit esse molestie \r\nconsequat, vel illum dolore eu feugiat nulla facilisis at vero eros et accumsan\r\net iusto odio dignissim qui blandit praesent luptatum zzril delenit augue duis dolore \r\nte feugait nulla facilisi. Nam liber tempor cum soluta nobis eleifend option congue nihil \r\nimperdiet doming id quod mazim placerat facer possim assum. Typi non habent claritatem insitam; \r\nest usus legentis in iis qui facit eorum claritatem. Investigationes demonstraverunt lectores \r\nlegere me lius quod ii legunt saepius. Claritas est etiam processus dynamicus, qui sequitur\r\nmutationem consuetudium lectorum. Mirum est notare quam littera gothica, quam nunc putamus \r\nparum claram, anteposuerit litterarum formas humanitatis per seacula quarta decima et quinta \r\ndecima. Eodem modo typi, qui nunc nobis videntur parum clari, fiant sollemnes in futurum.";
            var b = "Lorem ipsum dolor sit amet, adipiscing elit, \r\nsed diam nonummy nibh euismod tincidunt ut laoreet dolore vobiscum magna \r\naliquam erat volutpat. Ut wisi enim ad minim veniam, quis nostrud exerci \r\ntation ullamcorper suscipit lobortis nisl ut aliquip ex ea commodo consequat. \r\nDuis autem vel eum iriure dolor in hendrerit in vulputate velit esse molestie \r\nconsequat, vel illum dolore eu feugiat nulla facilisis at vero eros et accumsan\r\net iusto odio dignissim qui blandit praesent luptatum zzril delenit augue duis dolore \r\nte feugait nulla facilisi. Nam liber tempor cum soluta nobis eleifend option congue nihil \r\nimperdiet doming id quod mazim placerat facer possim assum. Typi non habent claritatem insitam; \r\nest usus legentis in iis qui facit eorum claritatem. Investigationes demonstraverunt lectores \r\nlegere me lius quod ii legunt saepius. Claritas est etiam processus dynamicus, qui sequitur\r\nmutationem consuetudium lectorum. Mirum est notare quam littera gothica, putamus \r\nparum claram, anteposuerit litterarum formas humanitatis per seacula quarta decima et quinta \r\ndecima. Eodem modo typi, qui nunc nobis videntur parum clari, fiant sollemnes in futurum.";
            var collection = Diff.Compute(a,b, 5);
            var p = Patch.FromDiffs(collection);
            var result = p.Apply(a);
            Assert.AreEqual(b, result.Item1);
        }

        [TestMethod]
        public void Timeout()
        {
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
            var timeout = TimeSpan.FromMilliseconds(100);

            using (var cts = new CancellationTokenSource(timeout))
            {
                var stopWatch = Stopwatch.StartNew();
                Diff.Compute(a, b, false, cts.Token, false);
                var elapsed = stopWatch.Elapsed;
                // assert that elapsed time is between timeout and 2*timeout (be forgiving)
                Assert.IsTrue(timeout <= elapsed.Add(TimeSpan.FromMilliseconds(1)), string.Format("Expected timeout < elapsed. Elapsed = {0}, Timeout = {1}.", elapsed, timeout));
                Assert.IsTrue(TimeSpan.FromTicks(2 * timeout.Ticks) > elapsed);
            }
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