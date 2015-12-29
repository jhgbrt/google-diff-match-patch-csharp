using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class DiffList_CleanupEfficiencyTests
    {
        [TestMethod]
        public void EmptyList()
        {
            var diffs = new List<Diff>();
            diffs.CleanupEfficiency();
            CollectionAssert.AreEqual(new List<Diff>(), diffs);
        }

        [TestMethod]
        public void NoElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("12"),
                Diff.Equal("wxyz"),
                Diff.Delete("cd"),
                Diff.Insert("34")
            };
            diffs.CleanupEfficiency();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("12"),
                Diff.Equal("wxyz"),
                Diff.Delete("cd"),
                Diff.Insert("34")
            }, diffs);            
        }

        [TestMethod]
        public void FourEditElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("12"),
                Diff.Equal("xyz"),
                Diff.Delete("cd"),
                Diff.Insert("34")
            };
            diffs.CleanupEfficiency();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("abxyzcd"),
                Diff.Insert("12xyz34")
            }, diffs);            
        }

        [TestMethod]
        public void ThreeEditElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Insert("12"),
                Diff.Equal("x"),
                Diff.Delete("cd"),
                Diff.Insert("34")
            };
            diffs.CleanupEfficiency();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("xcd"),
                Diff.Insert("12x34")
            }, diffs);            
        }

        [TestMethod]
        public void BackpassElimination()
        {
            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("12"),
                Diff.Equal("xy"),
                Diff.Insert("34"),
                Diff.Equal("z"),
                Diff.Delete("cd"),
                Diff.Insert("56")
            };
            diffs.CleanupEfficiency();
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("abxyzcd"),
                Diff.Insert("12xy34z56")
            }, diffs);            
        }

        [TestMethod]
        public void HighCostElimination()
        {
            short highDiffEditCost = 5;

            var diffs = new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("12"),
                Diff.Equal("wxyz"),
                Diff.Delete("cd"),
                Diff.Insert("34")
            };
            diffs.CleanupEfficiency(highDiffEditCost);
            CollectionAssert.AreEqual(new List<Diff>
            {
                Diff.Delete("abwxyzcd"),
                Diff.Insert("12wxyz34")
            }, diffs);
        }
    }
}