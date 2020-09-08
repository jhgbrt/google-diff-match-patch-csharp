using System.Collections.Generic;
using Xunit;

namespace DiffMatchPatch.Tests
{
    
    public class DiffList_CleanupSemanticTests
    {
        [Fact]
        public void EmptyList()
        {
            var diffs = new List<Diff>().CleanupSemantic();
            Assert.Equal(new List<Diff>(), diffs);
        }

        [Fact]
        public void NoEliminiation1()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("cd"),
                Diff.Equal("12"),
                Diff.Delete("e")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("cd"),
                Diff.Equal("12"),
                Diff.Delete("e")
            }, diffs);
        }

        [Fact]
        public void NoElimination2()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Insert("ABC"),
                Diff.Equal("1234"),
                Diff.Delete("wxyz")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Insert("ABC"),
                Diff.Equal("1234"),
                Diff.Delete("wxyz")
            }, diffs);
        }

        [Fact]
        public void SimpleElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("a"),
                Diff.Equal("b"),
                Diff.Delete("c")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Insert("b")
            }, diffs);            
        }

        

        [Fact]
        public void BackpassElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Equal("cd"),
                Diff.Delete("e"),
                Diff.Equal("f"),
                Diff.Insert("g")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abcdef"),
                Diff.Insert("cdfg")
            }, diffs);

        }

        [Fact]
        public void MultipleEliminations()
        {
            var diffs = new List<Diff>
            {
                Diff.Insert("1"),
                Diff.Equal("A"),
                Diff.Delete("B"),
                Diff.Insert("2"),
                Diff.Equal("_"),
                Diff.Insert("1"),
                Diff.Equal("A"),
                Diff.Delete("B"),
                Diff.Insert("2")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("AB_AB"),
                Diff.Insert("1A2_1A2")
            }, diffs);
        }

        [Fact]
        public void WordBoundaries()
        {
            var diffs = new List<Diff>
            {
                Diff.Equal("The c"),
                Diff.Delete("ow and the c"),
                Diff.Equal("at.")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("The "),
                Diff.Delete("cow and the "),
                Diff.Equal("cat.")
            }, diffs);
        }

        [Fact]
        public void NoOverlapElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abcxx"),
                Diff.Insert("xxdef")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abcxx"),
                Diff.Insert("xxdef")
            }, diffs);
        }

        [Fact]
        public void OverlapElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abcxxx"),
                Diff.Insert("xxxdef")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Equal("xxx"),
                Diff.Insert("def")
            }, diffs);
        }

        [Fact]
        public void ReverseOverlapElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("xxxabc"),
                Diff.Insert("defxxx")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Insert("def"),
                Diff.Equal("xxx"),
                Diff.Delete("abc")
            }, diffs);
        }

        [Fact]
        public void TwoOverlapEliminations()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abcd1212"),
                Diff.Insert("1212efghi"),
                Diff.Equal("----"),
                Diff.Delete("A3"),
                Diff.Insert("3BC")
            }.CleanupSemantic();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abcd"),
                Diff.Equal("1212"),
                Diff.Insert("efghi"),
                Diff.Equal("----"),
                Diff.Delete("A"),
                Diff.Equal("3"),
                Diff.Insert("BC")
            }, diffs);
        }


    }
}