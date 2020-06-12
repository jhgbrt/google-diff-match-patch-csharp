using System.Collections.Generic;
using Xunit;

namespace DiffMatchPatch.Tests
{
    
    public class BitapAlgorithmTests
    {
        [Fact]
        public void InitAlphabet_UniqueSet_ReturnsExpectedBitmask()
        {
            var bitmask = new Dictionary<char, int>
            {
                {'a', 4},
                {'b', 2},
                {'c', 1}
            };
            Assert.Equal(bitmask, BitapAlgorithm.InitAlphabet("abc"));

        }

        [Fact]
        public void InitAlphabet_SetWithDuplicates_ReturnsExpectedBitmask()
        {

            var bitmask = new Dictionary<char, int>
            {
                {'a', 37},
                {'b', 18},
                {'c', 8}
            };
            Assert.Equal(bitmask, BitapAlgorithm.InitAlphabet("abcaba"));
        }

        [Fact]
        public void Match_Exact1()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(5, dmp.Match("abcdefghijk", "fgh", 5));
        }
        [Fact]
        public void Match_Exact2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(5, dmp.Match("abcdefghijk", "fgh", 0));
        }


        [Fact]
        public void Match_Fuzzy1()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(4, dmp.Match("abcdefghijk", "efxhi", 0));
        }
        [Fact]
        public void Match_Fuzzy2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(2, dmp.Match("abcdefghijk", "cdefxyhijk", 5));
        }
        [Fact]
        public void Match_Fuzzy3()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(-1, dmp.Match("abcdefghijk", "bxy", 1));
        }

        [Fact]
        public void Match_Overflow()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(2, dmp.Match("123456789xx0", "3456789x0", 2));
        }


        [Fact]
        public void Match_BeforeStartMatch()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(0, dmp.Match("abcdef", "xxabc", 4));
        }

        [Fact]
        public void Match_BeyondEndMatch()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(3, dmp.Match("abcdef", "defyy", 4));
        }

        [Fact]
        public void Match_OversizedPattern()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(0, dmp.Match("abcdef", "xabcdefy", 0));
        }

        [Fact]
        public void Match_Treshold1()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.4f, 100));
            Assert.Equal(4, dmp.Match("abcdefghijk", "efxyhi", 1));

        }
        [Fact]
        public void Match_Treshold2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.3f, 100));
            Assert.Equal(-1, dmp.Match("abcdefghijk", "efxyhi", 1));

        }
        [Fact]
        public void Match_Treshold3()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.0f, 100));
            Assert.Equal(1, dmp.Match("abcdefghijk", "bcdef", 1));

        }

        [Fact]
        public void Match_MultipleSelect1()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(0, dmp.Match("abcdexyzabcde", "abccde", 3));
        }
        [Fact]
        public void Match_MultipleSelect2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.Equal(8, dmp.Match("abcdexyzabcde", "abccde", 5));
        }

        [Fact]
        public void Match_DistanceTest1()
        {

            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 10));
            Assert.Equal(-1, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdefg", 24));
        }

        [Fact]
        public void Match_DistanceTest2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 10));
            Assert.Equal(0, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdxxefg", 1));
        }
        [Fact]
        public void Match_DistanceTest3()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 1000));
            Assert.Equal(0, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdefg", 24));
        }

    }
}