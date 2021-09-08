using System.Collections.Generic;
using System.Threading;

using Xunit;

namespace DiffMatchPatch.Tests
{

    public class DiffListTests
    {
        [Fact]
        public void DiffPrettyHtmlTest()
        {

            // Pretty print.
            var diffs = new List<Diff>
            {
                Diff.Equal("a\n"),
                Diff.Delete("<B>b</B>"),
                Diff.Insert("c&d")
            };
            Assert.Equal(
                "<span>a&para;<br></span><del style=\"background:#ffe6e6;\">&lt;B&gt;b&lt;/B&gt;</del><ins style=\"background:#e6ffe6;\">c&amp;d</ins>",
                diffs.PrettyHtml());
        }

        [Fact]
        public void Text1_ReturnsText1()
        {
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
            Assert.Equal("jumps over the lazy", diffs.Text1());
        }

        [Fact]
        public void Text2_ReturnsText2()
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
            Assert.Equal("jumped over a lazy", diffs.Text2());
        }

        [Fact]
        public void FindEquivalentLocation2_LocationInEquality_FindsLocation()
        {

            // Translate a location in text1 to text2.
            var diffs = new List<Diff>
            {
                Diff.Delete("a"),
                Diff.Insert("1234"),
                Diff.Equal("xyz")
            };
            Assert.Equal(5, diffs.FindEquivalentLocation2(2));
        }
        [Fact]
        public void FindEquivalentLocation2_LocationOnDeletion_FindsLocation()
        {

            var diffs = new List<Diff>
            {
                Diff.Equal("a"),
                Diff.Delete("1234"),
                Diff.Equal("xyz")
            };
            Assert.Equal(1, diffs.FindEquivalentLocation2(3));
        }

        [Fact]
        public void Levenshtein_WithTrailingEquality()
        {

            var diffs = new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Insert("1234"),
                Diff.Equal("xyz")
            };
            Assert.Equal(4, diffs.Levenshtein());
        }
        [Fact]
        public void Levenshtein_WithLeadingEquality()
        {
            var diffs = new List<Diff>
            {
                Diff.Equal("xyz"),
                Diff.Delete("abc"),
                Diff.Insert("1234")
            };
            Assert.Equal(4, diffs.Levenshtein());

        }
        [Fact]
        public void Levenshtein_WithMiddleEquality()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Equal("xyz"),
                Diff.Insert("1234")
            };
            Assert.Equal(7, diffs.Levenshtein());
        }
        [Fact]
        public void DiffBisectTest_NoTimeout()
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
            Assert.Equal(diffs, DiffAlgorithm.MyersDiffBisect(a, b, false, new CancellationToken()));
        }


        [Fact]
        public void DiffBisectTest_WithTimeout()
        {
            var a = "cat";
            var b = "map";

            var diffs = new List<Diff> { Diff.Delete("cat"), Diff.Insert("map") };
            Assert.Equal(diffs, DiffAlgorithm.MyersDiffBisect(a, b, true, new CancellationToken(true)));
        }
    }
}