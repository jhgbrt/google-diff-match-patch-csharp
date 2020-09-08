using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace DiffMatchPatch.Tests
{
    
    public class DiffList_CleanupMergeTests
    {
        [Fact]
        public void CleanupMerge_EmptyDiffList_ReturnsEmptyDiffList()
        {
            // Cleanup a messy diff.
            // Null case.
            var result = new List<Diff>().CleanupMerge().ToList();
            var expected = new List<Diff>();
            
            Assert.Equal(expected, result);
        }

        [Fact]
        public void CleanupMerge_AlreadyCleaned_ReturnsSameList()
        {
            var result = new List<Diff> { Diff.Equal("a"), Diff.Delete("b"), Diff.Insert("c") }.CleanupMerge().ToList();
            
            var expected = new[] {Diff.Equal("a"), Diff.Delete("b"), Diff.Insert("c")};

            Assert.Equal(expected, result);
        }

        [Fact]
        public void SubsequentEqualitiesAreMerged()
        {
            var diffs = new List<Diff> { Diff.Equal("a"), Diff.Equal("b"), Diff.Equal("c") }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Equal("abc") }, diffs);
        }

        [Fact]
        public void SubsequentDeletesAreMerged()
        {
            var diffs = new List<Diff> { Diff.Delete("a"), Diff.Delete("b"), Diff.Delete("c") }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Delete("abc") }, diffs);
        }

        [Fact]
        public void SubsequentInsertsAreMerged()
        {
            var diffs = new List<Diff> { Diff.Insert("a"), Diff.Insert("b"), Diff.Insert("c") }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Insert("abc") }, diffs);
        }

        [Fact]
        public void InterweavedInsertDeletesAreMerged()
        {

            // Merge interweave.
            var diffs = new List<Diff>
            {
                Diff.Delete("a"),
                Diff.Insert("b"),
                Diff.Delete("c"),
                Diff.Insert("d"),
                Diff.Equal("e"),
                Diff.Equal("f")
            }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Delete("ac"), Diff.Insert("bd"), Diff.Equal("ef") }, diffs);
        }


        [Fact]
        public void PrefixSuffixDetection()
        {

            // Prefix and suffix detection.
            var diffs = new List<Diff> { Diff.Delete("a"), Diff.Insert("abc"), Diff.Delete("dc") }.CleanupMerge().ToList();
            Assert.Equal(
                new List<Diff> { Diff.Equal("a"), Diff.Delete("d"), Diff.Insert("b"), Diff.Equal("c") }, diffs);
        }
        [Fact]
        public void PrefixSuffixDetectionWithEqualities()
        {

            // Prefix and suffix detection.
            var diffs = new List<Diff>
            {
                Diff.Equal("x"),
                Diff.Delete("a"),
                Diff.Insert("abc"),
                Diff.Delete("dc"),
                Diff.Equal("y")
            }.CleanupMerge().ToList();
            Assert.Equal(
                new List<Diff> { Diff.Equal("xa"), Diff.Delete("d"), Diff.Insert("b"), Diff.Equal("cy") }, diffs);
        }
        

        [Fact]
        public void SlideEditLeft()
        {
            // Slide edit left.
            var diffs = new List<Diff> { Diff.Equal("a"), Diff.Insert("ba"), Diff.Equal("c") }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Insert("ab"), Diff.Equal("ac") }, diffs);
        }


        [Fact]
        public void SlideEditRight()
        {
 
            // Slide edit right.
            var diffs = new List<Diff> { Diff.Equal("c"), Diff.Insert("ab"), Diff.Equal("a") }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Equal("ca"), Diff.Insert("ba") }, diffs);

        }


        [Fact]
        public void SlideEditLeftRecursive()
        {
            // Slide edit left recursive.
            var diffs = new List<Diff>
            {
                Diff.Equal("a"),
                Diff.Delete("b"),
                Diff.Equal("c"),
                Diff.Delete("ac"),
                Diff.Equal("x")
            }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Delete("abc"), Diff.Equal("acx") }, diffs);

        }


        [Fact]
        public void SlideEditRightRecursive()
        {
            // Slide edit right recursive.
            var diffs = new List<Diff>
            {
                Diff.Equal("x"),
                Diff.Delete("ca"),
                Diff.Equal("c"),
                Diff.Delete("b"),
                Diff.Equal("a")
            }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Equal("xca"), Diff.Delete("cba") }, diffs);
        }

        [Fact]
        public void EmptyMerge()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("b"),
                Diff.Insert("ab"),
                Diff.Equal("c")
            }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Insert("a"), Diff.Equal("bc") }, diffs);
        }

        [Fact]
        public void EmptyEquality()
        {
            var diffs = new List<Diff>
            {
                Diff.Equal(""),
                Diff.Insert("a"),
                Diff.Equal("b")
            }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff> { Diff.Insert("a"), Diff.Equal("b") }, diffs);

        }

        [Fact]
        public void FourEditElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("12"),
                Diff.Equal("xyz"),
                Diff.Delete("cd"),
                Diff.Insert("34")
            }.CleanupMerge().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("12"),
                Diff.Equal("xyz"),
                Diff.Delete("cd"),
                Diff.Insert("34")
            }, diffs);
        }

    }
    
}