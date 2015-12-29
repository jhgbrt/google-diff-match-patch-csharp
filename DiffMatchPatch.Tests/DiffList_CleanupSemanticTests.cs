using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class DiffList_CleanupSemanticTests
    {
        [TestMethod]
        public void EmptyList()
        {
            var diffs = new List<Diff>();
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>(), diffs);
        }

        [TestMethod]
        public void NoEliminiation1()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("cd"),
                Diff.Equal("12"),
                Diff.Delete("e")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("cd"),
                Diff.Equal("12"),
                Diff.Delete("e")
            }, diffs);
        }

        [TestMethod]
        public void NoElimination2()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Insert("ABC"),
                Diff.Equal("1234"),
                Diff.Delete("wxyz")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Insert("ABC"),
                Diff.Equal("1234"),
                Diff.Delete("wxyz")
            }, diffs);
        }

        [TestMethod]
        public void SimpleElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("a"),
                Diff.Equal("b"),
                Diff.Delete("c")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Insert("b")
            }, diffs);            
        }

        

        [TestMethod]
        public void BackpassElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Equal("cd"),
                Diff.Delete("e"),
                Diff.Equal("f"),
                Diff.Insert("g")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("abcdef"),
                Diff.Insert("cdfg")
            }, diffs);

        }

        [TestMethod]
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
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("AB_AB"),
                Diff.Insert("1A2_1A2")
            }, diffs);
        }

        [TestMethod]
        public void WordBoundaries()
        {
            var diffs = new List<Diff>
            {
                Diff.Equal("The c"),
                Diff.Delete("ow and the c"),
                Diff.Equal("at.")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Equal("The "),
                Diff.Delete("cow and the "),
                Diff.Equal("cat.")
            }, diffs);
        }

        [TestMethod]
        public void NoOverlapElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abcxx"),
                Diff.Insert("xxdef")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("abcxx"),
                Diff.Insert("xxdef")
            }, diffs);
        }

        [TestMethod]
        public void OverlapElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abcxxx"),
                Diff.Insert("xxxdef")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("abc"),
                Diff.Equal("xxx"),
                Diff.Insert("def")
            }, diffs);
        }

        [TestMethod]
        public void ReverseOverlapElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("xxxabc"),
                Diff.Insert("defxxx")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Insert("def"),
                Diff.Equal("xxx"),
                Diff.Delete("abc")
            }, diffs);
        }

        [TestMethod]
        public void TwoOverlapEliminations()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("abcd1212"),
                Diff.Insert("1212efghi"),
                Diff.Equal("----"),
                Diff.Delete("A3"),
                Diff.Insert("3BC")
            };
            diffs.CleanupSemantic();
            CollectionAssert.AreEqual(new List<Diff>
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