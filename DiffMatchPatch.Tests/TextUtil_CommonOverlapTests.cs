using Microsoft.VisualStudio.TestTools.UnitTesting;
using static DiffMatchPatch.TextUtil;

namespace nicTest
{
    [TestClass]
    public class TextUtilTests
    {
        [TestMethod]
        public void CommonOverlap_EmptyString_NoOverlap()
        {
            // Detect any suffix/prefix overlap.
            // Null case.
            Assert.AreEqual(0, CommonOverlap("", "abcd"));
        }
        [TestMethod]
        public void CommonOverlap_FirstIsPrefixOfSecond_FullOverlap()
        {

            // Whole case.
            Assert.AreEqual(3, CommonOverlap("abc", "abcd"));
        }
        [TestMethod]
        public void CommonOverlap_DisjunctStrings_NoOverlap()
        {

            // No overlap.
            Assert.AreEqual(0, CommonOverlap("123456", "abcd"));
        }
        [TestMethod]
        public void CommonOverlap_FirstEndsWithStartOfSecond_Overlap()
        {

            // Overlap.
            Assert.AreEqual(3, CommonOverlap("123456xxx", "xxxabcd"));
        }
        [TestMethod]
        public void CommonOverlap_UnicodeLigaturesAndComponentLetters_NoOverlap()
        {
            // Unicode.
            // Some overly clever languages (C#) may treat ligatures as equal to their
            // component letters.  E.g. U+FB01 == 'fi'
            Assert.AreEqual(0, CommonOverlap("fi", "\ufb01i"));
        }

        [TestMethod]
        public void CommonPrefix_DisjunctStrings_NoCommonPrefix()
        {
            // Detect any common suffix.
            // Null case.
            Assert.AreEqual(0, CommonPrefix("abc", "xyz"));
        }

        [TestMethod]
        public void CommonPrefix_BothStringsStartWithSame_CommonPrefixIsDetected()
        {
            // Non-null case.
            Assert.AreEqual(4, CommonPrefix("1234abcdef", "1234xyz"));
        }

        [TestMethod]
        public void CommonPrefix_FirstStringIsSubstringOfSecond_CommonPrefixIsDetected()
        {

            // Whole case.
            Assert.AreEqual(4, CommonPrefix("1234", "1234xyz"));
        }

        [TestMethod]
        public void CommonSuffix_DisjunctStrings_NoCommonSuffix()
        {
            // Detect any common suffix.
            // Null case.
            Assert.AreEqual(0, CommonSuffix("abc", "xyz"));
        }

        [TestMethod]
        public void CommonSuffix_BothStringsEndWithSame_CommonSuffixIsDetected()
        {
            // Non-null case.
            Assert.AreEqual(4, CommonSuffix("abcdef1234", "xyz1234"));
        }

        [TestMethod]
        public void CommonSuffix_FirstStringIsSubstringOfSecond_CommonSuffixIsDetected()
        {
            // Whole case.
            Assert.AreEqual(4, CommonSuffix("1234", "xyz1234"));
        }
    }
}