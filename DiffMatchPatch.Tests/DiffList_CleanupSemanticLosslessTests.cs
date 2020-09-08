using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DiffMatchPatch.Tests
{
    
    public class DiffList_CleanupSemanticLosslessTests
    {
        [Fact]
        public void EmptyList_WhenCleaned_RemainsEmptyList()
        {
            // Slide Diffs to match logical boundaries.
            // Null case.
            var diffs = new List<Diff>().CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>(), diffs);
        }

        [Fact]
        public void SingleDiff_WhenCleaned_Remains()
        {
            // Blank lines.
            var diffs = new List<Diff>
            {
                Diff.Equal("AAA"),
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("AAA"),
            }, diffs);
        }

        [Fact]
        public void TwoDiffs_WhenCleaned_Remains()
        {
            // Blank lines.
            var diffs = new List<Diff>
            {
                Diff.Equal("AAA"),
                Diff.Insert("BBB"),
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("AAA"),
                Diff.Insert("BBB"),
            }, diffs);
        }
        [Fact]
        public void ThreeDiffs_WhenCleaned_Remains()
        {
            // Blank lines.
            var diffs = new List<Diff>
            {
                Diff.Equal("AAA"),
                Diff.Insert("BBB"),
                Diff.Delete("CCC"),
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("AAA"),
                Diff.Insert("BBB"),
                Diff.Delete("CCC"),
            }, diffs);
        }
        [Fact]
        public void FourDiffs_WhenCleaned_Remains()
        {
            // Blank lines.
            var diffs = new List<Diff>
            {
                Diff.Equal("AAA"),
                Diff.Insert("BBB"),
                Diff.Delete("CCC"),
                Diff.Equal("DDD"),
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("AAA"),
                Diff.Insert("BBB"),
                Diff.Delete("CCC"),
                Diff.Equal("DDD"),
            }, diffs);
        }


        [Fact]
        public void BlankLines()
        {
            // Blank lines.
            var diffs = new List<Diff>
            {
                Diff.Equal("AAA\r\n\r\nBBB"),
                Diff.Insert("\r\nDDD\r\n\r\nBBB"),
                Diff.Equal("\r\nEEE")
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("AAA\r\n\r\n"),
                Diff.Insert("BBB\r\nDDD\r\n\r\n"),
                Diff.Equal("BBB\r\nEEE")
            }, diffs);
        }

        [Fact]
        public void NoCleanup()
        {
            // Line boundaries.
            var diffs = new List<Diff>
            {
                Diff.Equal("AAA\r\n"),
                Diff.Insert("BBB DDD\r\n"),
                Diff.Equal("BBB EEE\r\n"),
                Diff.Insert("FFF GGG\r\n"),
                Diff.Equal("HHH III"),
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("AAA\r\n"),
                Diff.Insert("BBB DDD\r\n"),
                Diff.Equal("BBB EEE\r\n"),
                Diff.Insert("FFF GGG\r\n"),
                Diff.Equal("HHH III"),
            }, diffs);

        }

        [Fact]
        public void LineBoundaries()
        {

            // Line boundaries.
            var diffs = new List<Diff>
            {
                Diff.Equal("AAA\r\nBBB"),
                Diff.Insert(" DDD\r\nBBB"),
                Diff.Equal(" EEE")
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("AAA\r\n"),
                Diff.Insert("BBB DDD\r\n"),
                Diff.Equal("BBB EEE")
            }, diffs);
        }

        [Fact]
        public void WordBoundaries()
        {
            var diffs = new List<Diff>
            {
                Diff.Equal("The c"),
                Diff.Insert("ow and the c"),
                Diff.Equal("at.")
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("The "),
                Diff.Insert("cow and the "),
                Diff.Equal("cat.")
            }, diffs);
        }

        [Fact]
        public void AlphaNumericBoundaries()
        {
            // Alphanumeric boundaries.
            var diffs = new List<Diff>
            {
                Diff.Equal("The-c"),
                Diff.Insert("ow-and-the-c"),
                Diff.Equal("at.")
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("The-"),
                Diff.Insert("cow-and-the-"),
                Diff.Equal("cat.")
            }, diffs);
        }

        [Fact]
        public void HittingTheStart()
        {
            var diffs = new List<Diff>
            {
                Diff.Equal("a"),
                Diff.Delete("a"),
                Diff.Equal("ax")
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Delete("a"),
                Diff.Equal("aax")
            }, diffs);
        }

        [Fact]
        public void HittingTheEnd()
        {
            var diffs = new List<Diff>
            {
                Diff.Equal("xa"),
                Diff.Delete("a"),
                Diff.Equal("a")
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("xaa"),
                Diff.Delete("a")
            }, diffs);
        }

        [Fact]
        public void SentenceBoundaries()
        {
            var diffs = new List<Diff>
            {
                Diff.Equal("The xxx. The "),
                Diff.Insert("zzz. The "),
                Diff.Equal("yyy.")
            }.CleanupSemanticLossless().ToList();
            Assert.Equal(new List<Diff>
            {
                Diff.Equal("The xxx."),
                Diff.Insert(" The zzz."),
                Diff.Equal(" The yyy.")
            }, diffs);
        }

       
    }
}