using System.Collections.Generic;
using Xunit;

namespace DiffMatchPatch.Tests
{
    
    public class DiffList_CleanupEfficiencyTests
    {
        [Fact]
        public void EmptyList()
        {
            var diffs = new List<Diff>();
            diffs.CleanupEfficiency();
            Assert.Equal(new List<Diff>(), diffs);
        }

        [Fact]
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
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("ab"),
                Diff.Insert("12"),
                Diff.Equal("wxyz"),
                Diff.Delete("cd"),
                Diff.Insert("34")
            }, diffs);            
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
            };
            diffs.CleanupEfficiency();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abxyzcd"),
                Diff.Insert("12xyz34")
            }, diffs);            
        }

        [Fact]
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
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("xcd"),
                Diff.Insert("12x34")
            }, diffs);            
        }

        [Fact]
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
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abxyzcd"),
                Diff.Insert("12xy34z56")
            }, diffs);            
        }

        [Fact]
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
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("abwxyzcd"),
                Diff.Insert("12wxyz34")
            }, diffs);
        }
    }
}