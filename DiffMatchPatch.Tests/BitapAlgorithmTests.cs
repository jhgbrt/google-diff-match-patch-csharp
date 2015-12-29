using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class BitapAlgorithmTests
    {
        [TestMethod]
        public void InitAlphabet_UniqueSet_ReturnsExpectedBitmask()
        {
            var bitmask = new Dictionary<char, int>
            {
                {'a', 4},
                {'b', 2},
                {'c', 1}
            };
            CollectionAssert.AreEqual(bitmask, BitapAlgorithm.InitAlphabet("abc"), "match_alphabet: Unique.");

        }

        [TestMethod]
        public void InitAlphabet_SetWithDuplicates_ReturnsExpectedBitmask()
        {

            var bitmask = new Dictionary<char, int>
            {
                {'a', 37},
                {'b', 18},
                {'c', 8}
            };
            CollectionAssert.AreEqual(bitmask, BitapAlgorithm.InitAlphabet("abcaba"), "match_alphabet: Duplicates.");
        }

        [TestMethod]
        public void Match_Exact1()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(5, dmp.Match("abcdefghijk", "fgh", 5), "match_bitap: Exact match #1.");
        }
        [TestMethod]
        public void Match_Exact2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(5, dmp.Match("abcdefghijk", "fgh", 0), "match_bitap: Exact match #2.");
        }


        [TestMethod]
        public void Match_Fuzzy1()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(4, dmp.Match("abcdefghijk", "efxhi", 0), "match_bitap: Fuzzy match #1.");
        }
        [TestMethod]
        public void Match_Fuzzy2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(2, dmp.Match("abcdefghijk", "cdefxyhijk", 5), "match_bitap: Fuzzy match #2.");
        }
        [TestMethod]
        public void Match_Fuzzy3()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(-1, dmp.Match("abcdefghijk", "bxy", 1), "match_bitap: Fuzzy match #3.");
        }

        [TestMethod]
        public void Match_Overflow()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(2, dmp.Match("123456789xx0", "3456789x0", 2), "match_bitap: Overflow.");
        }


        [TestMethod]
        public void Match_BeforeStartMatch()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(0, dmp.Match("abcdef", "xxabc", 4), "match_bitap: Before start match.");
        }

        [TestMethod]
        public void Match_BeyondEndMatch()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(3, dmp.Match("abcdef", "defyy", 4), "match_bitap: Beyond end match.");
        }

        [TestMethod]
        public void Match_OversizedPattern()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(0, dmp.Match("abcdef", "xabcdefy", 0), "match_bitap: Oversized pattern.");
        }

        [TestMethod]
        public void Match_Treshold1()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.4f, 100));
            Assert.AreEqual(4, dmp.Match("abcdefghijk", "efxyhi", 1), "match_bitap: Threshold #1.");

        }
        [TestMethod]
        public void Match_Treshold2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.3f, 100));
            Assert.AreEqual(-1, dmp.Match("abcdefghijk", "efxyhi", 1), "match_bitap: Threshold #2.");

        }
        [TestMethod]
        public void Match_Treshold3()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.0f, 100));
            Assert.AreEqual(1, dmp.Match("abcdefghijk", "bcdef", 1), "match_bitap: Threshold #3.");

        }

        [TestMethod]
        public void Match_MultipleSelect1()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(0, dmp.Match("abcdexyzabcde", "abccde", 3), "match_bitap: Multiple select #1.");
        }
        [TestMethod]
        public void Match_MultipleSelect2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 100));
            Assert.AreEqual(8, dmp.Match("abcdexyzabcde", "abccde", 5), "match_bitap: Multiple select #2.");
        }

        [TestMethod]
        public void Match_DistanceTest1()
        {

            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 10));
            Assert.AreEqual(-1, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdefg", 24),
                "match_bitap: Distance test #1.");
        }

        [TestMethod]
        public void Match_DistanceTest2()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 10));
            Assert.AreEqual(0, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdxxefg", 1),
                "match_bitap: Distance test #2.");
        }
        [TestMethod]
        public void Match_DistanceTest3()
        {
            var dmp = new BitapAlgorithm(new MatchSettings(0.5f, 1000));
            Assert.AreEqual(0, dmp.Match("abcdefghijklmnopqrstuvwxyz", "abcdefg", 24),
                "match_bitap: Distance test #3.");
        }

    }
}