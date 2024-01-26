using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace DiffMatchPatch.Tests
{
    public class PatchTests
    {


        [Fact]
        public void ToString_ReturnsExpectedString()
        {
            // Patch Object.
            var p = new Patch(20, 18, 21, 17, new[]
            {
                Diff.Equal("jump"),
                Diff.Delete("s"),
                Diff.Insert("ed"),
                Diff.Equal(" over "),
                Diff.Delete("the"),
                Diff.Insert("a"),
                Diff.Equal("\nlaz")
            }.ToImmutableList());
            var strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n %0alaz\n";
            Assert.Equal(strp, p.ToString());
        }

        [Fact]
        public void Parse_EmptyString_YieldsEmptyList()
        {
            var patches = PatchList.Parse("");
            Assert.False(patches.Any());
        }

        [Fact]
        public void FromTextTest1()
        {

            var strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n %0alaz\n";
            Assert.Equal(strp, PatchList.Parse(strp)[0].ToString());
        }

        [Fact]
        public void FromTextTest2()
        {

            Assert.Equal("@@ -1 +1 @@\n-a\n+b\n", PatchList.Parse("@@ -1 +1 @@\n-a\n+b\n")[0].ToString());
        }

        [Fact]
        public void FromTextTest3()
        {

            Assert.Equal("@@ -1,3 +0,0 @@\n-abc\n", PatchList.Parse("@@ -1,3 +0,0 @@\n-abc\n")[0].ToString());
        }

        [Fact]
        public void FromTextTest4()
        {
            Assert.Equal("@@ -0,0 +1,3 @@\n+abc\n", PatchList.Parse("@@ -0,0 +1,3 @@\n+abc\n")[0].ToString());
        }

        [Fact]
        public void Parse_WhenBadPatch_Throws()
        {
            Assert.Throws<ArgumentException>(() => PatchList.Parse("Bad\nPatch\n"));
        }

        [Fact]
        public void ToText_Test1()
        {

            var strp = "@@ -21,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n  laz\n";
            var patches = PatchList.Parse(strp);
            var result = patches.ToText();
            Assert.Equal(strp, result);
        }

        [Fact]
        public void ToText_Test2()
        {
            var strp = "@@ -1,9 +1,9 @@\n-f\n+F\n oo+fooba\n@@ -7,9 +7,9 @@\n obar\n-,\n+.\n  tes\n";
            var patches = PatchList.Parse(strp);
            var result = patches.ToText();
            Assert.Equal(strp, result);
        }

        [Fact]
        public void AddContext_SimpleCase()
        {
            var p = PatchList.Parse("@@ -21,4 +21,10 @@\n-jump\n+somersault\n")[0];
            var builder = p.Diffs.ToBuilder();
            (int s1, int l1, int s2, int l2) = builder.AddContext("The quick brown fox jumps over the lazy dog.", p.Start1, p.Length1, p.Start2, p.Length2);
            p = new Patch(s1, l1, s2, l2, builder.ToImmutable());
            Assert.Equal("@@ -17,12 +17,18 @@\n fox \n-jump\n+somersault\n s ov\n", p.ToString());

        }

        [Fact]
        public void AddContext_NotEnoughTrailingContext()
        {
            var p = PatchList.Parse("@@ -21,4 +21,10 @@\n-jump\n+somersault\n")[0];
            var builder = p.Diffs.ToBuilder();
            (int s1, int l1, int s2, int l2) = builder.AddContext("The quick brown fox jumps.", p.Start1, p.Length1, p.Start2, p.Length2);
            p = new Patch(s1, l1, s2, l2, builder.ToImmutable());
            Assert.Equal("@@ -17,10 +17,16 @@\n fox \n-jump\n+somersault\n s.\n", p.ToString());
        }

        [Fact]
        public void AddContext_NotEnoughLeadingContext()
        {
            var p = PatchList.Parse("@@ -3 +3,2 @@\n-e\n+at\n")[0];
            var builder = p.Diffs.ToBuilder();
            (int s1, int l1, int s2, int l2) = builder.AddContext("The quick brown fox jumps.", p.Start1, p.Length1, p.Start2, p.Length2);
            p = new Patch(s1, l1, s2, l2, builder.ToImmutable());
            Assert.Equal("@@ -1,7 +1,8 @@\n Th\n-e\n+at\n  qui\n", p.ToString());
        }

        [Fact]
        public void AddContext_Ambiguity()
        {
            var p = PatchList.Parse("@@ -3 +3,2 @@\n-e\n+at\n")[0];
            var builder = p.Diffs.ToBuilder();
            (int s1, int l1, int s2, int l2) = builder.AddContext("The quick brown fox jumps.  The quick brown fox crashes.", p.Start1, p.Length1, p.Start2, p.Length2);
            p = new Patch(s1, l1, s2, l2, builder.ToImmutable());
            Assert.Equal("@@ -1,27 +1,28 @@\n Th\n-e\n+at\n  quick brown fox jumps. \n", p.ToString());
        }

        [Fact]
        public void Compute_FromEmptyString_Succeeds()
        {
            var patches = Patch.Compute("", "");
            Assert.Equal("", patches.ToText());
        }

        [Fact]
        public void Compute_FromTwoStrings_Reversed_Succeeds()
        {

            var text1 = "The quick brown fox jumps over the lazy dog.";
            var text2 = "That quick brown fox jumped over a lazy dog.";
            var expectedPatch =
                "@@ -1,8 +1,7 @@\n Th\n-at\n+e\n  qui\n@@ -21,17 +21,18 @@\n jump\n-ed\n+s\n  over \n-a\n+the\n  laz\n";
            // The second patch must be "-21,17 +21,18", not "-22,17 +21,18" due to rolling context.
            var patches = Patch.Compute(text2, text1);
            Assert.Equal(expectedPatch, patches.ToText());
        }

        [Fact]
        public void Compute_FromTwoStrings_Succeeds()
        {
            var text1 = "The quick brown fox jumps over the lazy dog.";
            var text2 = "That quick brown fox jumped over a lazy dog.";

            var expectedPatch =
                "@@ -1,11 +1,12 @@\n Th\n-e\n+at\n  quick b\n@@ -22,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n  laz\n";
            var patches = Patch.Compute(text1, text2);
            Assert.Equal(expectedPatch, patches.ToText());
        }

        [Fact]
        public void Compute_FromDiffs_Succeeds()
        {
            var text1 = "The quick brown fox jumps over the lazy dog.";
            var text2 = "That quick brown fox jumped over a lazy dog.";
            var diffs = Diff.Compute(text1, text2, 0, false);
            var patches = Patch.FromDiffs(diffs);
            var expectedPatch =
                "@@ -1,11 +1,12 @@\n Th\n-e\n+at\n  quick b\n@@ -22,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n  laz\n";
            Assert.Equal(expectedPatch, patches.ToText());
        }

        [Fact]
        public void Compute_FromTextAndDiffs_Succeeds()
        {
            var text1 = "The quick brown fox jumps over the lazy dog.";
            var text2 = "That quick brown fox jumped over a lazy dog.";
            var diffs = Diff.Compute(text1, text2, 0, false);
            var expectedPatch =
                "@@ -1,11 +1,12 @@\n Th\n-e\n+at\n  quick b\n@@ -22,18 +22,17 @@\n jump\n-s\n+ed\n  over \n-the\n+a\n  laz\n";
            var patches = Patch.Compute(text1, diffs).ToList();
            Assert.Equal(expectedPatch, patches.ToText());
        }

        [Fact]
        public void Compute_CharacterEncoding()
        {

            var patches = Patch.Compute("`1234567890-=[]\\;',./", "~!@#$%^&*()_+{}|:\"<>?");
            Assert.Equal(
                "@@ -1,21 +1,21 @@\n-%601234567890-=%5b%5d%5c;',./\n+~!@#$%25%5e&*()_+%7b%7d%7c:%22%3c%3e?\n",
                patches.ToText());
        }

        [Fact]
        public void Compute_CharacterDecoding()
        {


            var diffs = new List<Diff>
            {
                Diff.Delete("`1234567890-=[]\\;',./"),
                Diff.Insert("~!@#$%^&*()_+{}|:\"<>?")
            };
            Assert.Equal(diffs,
                PatchList.Parse("@@ -1,21 +1,21 @@\n-%601234567890-=%5B%5D%5C;',./\n+~!@#$%25%5E&*()_+%7B%7D%7C:%22%3C%3E?\n")[0]
                    .Diffs);
        }

        [Fact]
        public void Compute_LongStringWithRepeats()
        {
            var text1 = "";
            for (var x = 0; x < 100; x++)
            {
                text1 += "abcdef";
            }
            var text2 = text1 + "123";
            var expectedPatch = "@@ -573,28 +573,31 @@\n cdefabcdefabcdefabcdefabcdef\n+123\n";
            var patches = Patch.Compute(text1, text2);
            Assert.Equal(expectedPatch, patches.ToText());

        }

        [Fact]
        public void SplitMaxTest1()
        {
            var patches = Patch
                .Compute("abcdefghijklmnopqrstuvwxyz01234567890", "XabXcdXefXghXijXklXmnXopXqrXstXuvXwxXyzX01X23X45X67X89X0")
                .SplitMax()
                .ToImmutableList();
            Assert.Equal(
                "@@ -1,32 +1,46 @@\n+X\n ab\n+X\n cd\n+X\n ef\n+X\n gh\n+X\n ij\n+X\n kl\n+X\n mn\n+X\n op\n+X\n qr\n+X\n st\n+X\n uv\n+X\n wx\n+X\n yz\n+X\n 012345\n@@ -25,13 +39,18 @@\n zX01\n+X\n 23\n+X\n 45\n+X\n 67\n+X\n 89\n+X\n 0\n",
                patches.ToText());
        }

        [Fact]
        public void SplitMaxTest2()
        {
            var patches = Patch.Compute("abcdef1234567890123456789012345678901234567890123456789012345678901234567890uvwxyz", "abcdefuvwxyz");
            var oldToText = patches.ToText();
            var patches2 = patches.SplitMax().ToImmutableList();
            Assert.Equal(oldToText, patches2.ToText());
        }

        [Fact]
        public void SplitMaxTest3()
        {

            var patches = Patch
                .Compute("1234567890123456789012345678901234567890123456789012345678901234567890", "abc")
                .SplitMax()
                .ToImmutableList();
            Assert.Equal(
                "@@ -1,32 +1,4 @@\n-1234567890123456789012345678\n 9012\n@@ -29,32 +1,4 @@\n-9012345678901234567890123456\n 7890\n@@ -57,14 +1,3 @@\n-78901234567890\n+abc\n",
                patches.ToText());

        }

        [Fact]
        public void SplitMaxTest4()
        {
            var patches = Patch
                .Compute("abcdefghij , h : 0 , t : 1 abcdefghij , h : 0 , t : 1 abcdefghij , h : 0 , t : 1", "abcdefghij , h : 1 , t : 1 abcdefghij , h : 1 , t : 1 abcdefghij , h : 0 , t : 1")
                .SplitMax()
                .ToImmutableList();
            Assert.Equal(
                "@@ -2,32 +2,32 @@\n bcdefghij , h : \n-0\n+1\n  , t : 1 abcdef\n@@ -29,32 +29,32 @@\n bcdefghij , h : \n-0\n+1\n  , t : 1 abcdef\n",
                patches.ToText());
        }

        [Fact]
        public void AddPadding_Empty()
        {
            var patches = Patch.Compute("", "");
            Assert.Equal("", patches.ToText());
            var patches2 = patches.AddPadding(PatchList.NullPadding).ToImmutableList();
            Assert.Equal("", patches2.ToText());
        }

        [Fact]
        public void AddPadding_BothEdgesFull()
        {
            var patches = Patch.Compute("", "test");
            Assert.Equal("@@ -0,0 +1,4 @@\n+test\n", patches.ToText());
            var patches2 = patches.AddPadding(PatchList.NullPadding).ToImmutableList();
            Assert.Equal("@@ -1,8 +1,12 @@\n %01%02%03%04\n+test\n %01%02%03%04\n",
                patches2.ToText());
        }

        [Fact]
        public void AddPadding_BothEdgesPartial()
        {
            var patches = Patch.Compute("XY", "XtestY");
            Assert.Equal("@@ -1,2 +1,6 @@\n X\n+test\n Y\n",
                patches.ToText());
            var patches2 = patches.AddPadding(PatchList.NullPadding).ToImmutableList();
            Assert.Equal("@@ -2,8 +2,12 @@\n %02%03%04X\n+test\n Y%01%02%03\n",
                patches2.ToText());
        }

        [Fact]
        public void AddPadding_BothEdgesNone()
        {
            var patches = Patch.Compute("XXXXYYYY", "XXXXtestYYYY");
            Assert.Equal("@@ -1,8 +1,12 @@\n XXXX\n+test\n YYYY\n",
                patches.ToText());
            var patches2 = patches.AddPadding(PatchList.NullPadding).ToImmutableList();
            Assert.Equal("@@ -5,8 +5,12 @@\n XXXX\n+test\n YYYY\n",
                patches2.ToText());
        }

        [Fact]
        public void Apply_EmptyString()
        {
            var patches = Patch.Compute("", "");
            var results = patches.Apply("Hello world.");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray.Length;
            Assert.Equal("Hello world.\t0", resultStr);
        }

        [Fact]
        public void Apply_ExactMatch()
        {
            var patches = Patch.Compute("The quick brown fox jumps over the lazy dog.", "That quick brown fox jumped over a lazy dog.");
            var results = patches.Apply("The quick brown fox jumps over the lazy dog.");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.Equal("That quick brown fox jumped over a lazy dog.\tTrue\tTrue", resultStr);

        }

        [Fact]
        public void Apply_PartialMatch()
        {
            var patches = Patch.Compute("The quick brown fox jumps over the lazy dog.", "That quick brown fox jumped over a lazy dog.");
            var results = patches.Apply("The quick red rabbit jumps over the tired tiger.");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.Equal("That quick red rabbit jumped over a tired tiger.\tTrue\tTrue", resultStr);
                    }

        [Fact]
        public void Apply_FailedMatch()
        {

            var patches = Patch.Compute("The quick brown fox jumps over the lazy dog.", "That quick brown fox jumped over a lazy dog.");
            var results = patches.Apply("I am the very model of a modern major general.");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.Equal("I am the very model of a modern major general.\tFalse\tFalse", resultStr);
                    }

        [Fact]
        public void Apply_BigDeleteSmallChange()
        {
            var patches = Patch.Compute("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            var results = patches.Apply("x123456789012345678901234567890-----++++++++++-----123456789012345678901234567890y");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.Equal("xabcy\tTrue\tTrue", resultStr);
        }

        [Fact]
        public void Apply_BidgDeleteBigChange1()
        {

            var patches = Patch.Compute("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            var results = patches.Apply("x12345678901234567890---------------++++++++++---------------12345678901234567890y");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.Equal(
                "xabc12345678901234567890---------------++++++++++---------------12345678901234567890y\tFalse\tTrue",
                resultStr);

        }

        [Fact]
        public void Apply_BigDeleteBigChange2()
        {
            var patches = Patch.Compute("x1234567890123456789012345678901234567890123456789012345678901234567890y", "xabcy");
            var results = patches.Apply("x12345678901234567890---------------++++++++++---------------12345678901234567890y", MatchSettings.Default, new PatchSettings(0.6f, 4));
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.Equal("xabcy\tTrue\tTrue", resultStr);

        }

        [Fact]
        public void Apply_CompensateForFailedPatch()
        {
            var patches = Patch.Compute("abcdefghijklmnopqrstuvwxyz--------------------1234567890", "abcXXXXXXXXXXdefghijklmnopqrstuvwxyz--------------------1234567YYYYYYYYYY890");
            var results = patches.Apply("ABCDEFGHIJKLMNOPQRSTUVWXYZ--------------------1234567890", new MatchSettings(0.0f, 0));
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0] + "\t" + boolArray[1];
            Assert.Equal("ABCDEFGHIJKLMNOPQRSTUVWXYZ--------------------1234567YYYYYYYYYY890\tFalse\tTrue", resultStr);

        }

        [Fact]
        public void Apply_NoSideEffects()
        {
            var patches = Patch.Compute("", "test");
            var patchStr = patches.ToText();
            patches.Apply("");
            Assert.Equal(patchStr, patches.ToText());

        }

        [Fact]
        public void Apply_NoSideEffectsWithMajorDelete()
        {
            var patches = Patch.Compute("The quick brown fox jumps over the lazy dog.", "Woof");
            var patchStr = patches.ToText();
            patches.Apply("The quick brown fox jumps over the lazy dog.");
            Assert.Equal(patchStr, patches.ToText());
        }

        [Fact]
        public void Apply_EdgeExactMatch()
        {

            var patches = Patch.Compute("", "test");
            var results = patches.Apply("");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0];
            Assert.Equal("test\tTrue", resultStr);
        }

        [Fact]
        public void Apply_NearEdgeExactMatch()
        {

            var patches = Patch.Compute("XY", "XtestY");
            var results = patches.Apply("XY");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0];
            Assert.Equal("XtestY\tTrue", resultStr);

        }

        [Fact]
        public void Apply_EdgePartialMatch()
        {
            var patches = Patch.Compute("y", "y123");
            var results = patches.Apply("x");
            var boolArray = results.results;
            var resultStr = results.newText + "\t" + boolArray[0];
            Assert.Equal("x123\tTrue", resultStr);
        }

        [Fact]
        public void LargeEquality()
        {
            var diffs = new List<Diff>
            {
                Diff.Insert(" "),
                Diff.Equal("a"),
                Diff.Insert("nd"),
                Diff.Equal(" [[Pennsylvania]]"),
                Diff.Delete(" and [[New")
            };

            var patch = Patch.FromDiffs(diffs);

            Assert.Equal(diffs,
                Diff.Compute("a [[Pennsylvania]] and [[New", " and [[Pennsylvania]]", 0, false));
        }
    }
}