using Xunit;

namespace DiffMatchPatch.Tests
{
    public class HalfMatchResultTests
    {
        [Fact]
        public void HalfMatchResult_Reverse_ReversesPrefixAndSuffix()
        {
            var r = new HalfMatchResult("p1", "s1", "p2", "s2", "m").Reverse();
            Assert.Equal("p1", r.Prefix2);
            Assert.Equal("s1", r.Suffix2);
            Assert.Equal("p2", r.Prefix1);
            Assert.Equal("s2", r.Suffix1);
            Assert.Equal("m", r.CommonMiddle);
        }
        [Fact]
        public void HalfMatchResult_IsEmpty_WhenCommonMiddleNotEmpty_ReturnsFalse()
        {
            var r = new HalfMatchResult("p1", "s1", "p2", "s2", "m");
            Assert.False(r.IsEmpty);
        }
        [Fact]
        public void HalfMatchResult_IsEmpty_WhenCommonMiddleEmpty_ReturnsTrue()
        {
            var r = new HalfMatchResult("p1", "s1", "p2", "s2", "");
            Assert.True(r.IsEmpty);
        }
        [Fact]
        public void HalfMatchResult_LargerThan_SmallerThan()
        {
            var r1 = HalfMatchResult.Empty with { CommonMiddle = "abc" };
            var r2 = HalfMatchResult.Empty with { CommonMiddle = "abcd" };
            Assert.True(r2 > r1);
            Assert.True(r1 < r2);
        }
    }
}