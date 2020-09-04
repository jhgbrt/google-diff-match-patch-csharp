using Xunit;

namespace DiffMatchPatch.Tests
{
    
    public class TextUtilTests
    {
        [Fact]
        public void CommonOverlapEmptyStringNoOverlap()
        {
            // Detect any suffix/prefix overlap.
            // Null case.
            Assert.Equal(0, TextUtil.CommonOverlap("", "abcd"));
        }
        [Fact]
        public void CommonOverlapFirstIsPrefixOfSecondFullOverlap()
        {

            // Whole case.
            Assert.Equal(3, TextUtil.CommonOverlap("abc", "abcd"));
        }
        [Fact]
        public void CommonOverlapRecurringPatternOverlap()
        {

            Assert.Equal(4, TextUtil.CommonOverlap("xyz1212", "1212abc"));
        }
        [Fact]
        public void CommonOverlapDisjunctStringsNoOverlap()
        {

            // No overlap.
            Assert.Equal(0, TextUtil.CommonOverlap("123456", "abcd"));
        }
        [Fact]
        public void CommonOverlapPatternInTheMiddle_NoOverlap()
        {

            Assert.Equal(0, TextUtil.CommonOverlap("123456xxx", "efgxxxabcd"));
        }
        [Fact]
        public void CommonOverlapFirstEndsWithStartOfSecondOverlap()
        {

            // Overlap.
            Assert.Equal(3, TextUtil.CommonOverlap("123456xyz", "xyzabcd"));
        }
        [Fact]
        public void CommonOverlapUnicodeLigaturesAndComponentLettersNoOverlap()
        {
            // Unicode.
            // Some overly clever languages (C#) may treat ligatures as equal to their
            // component letters.  E.g. U+FB01 == 'fi'
            Assert.Equal(0, TextUtil.CommonOverlap("fi", "\ufb01i"));
        }

        [Fact]
        public void CommonPrefixDisjunctStringsNoCommonPrefix()
        {
            // Detect any common suffix.
            // Null case.
            Assert.Equal(0, TextUtil.CommonPrefix("abc", "xyz"));
        }

        [Fact]
        public void CommonPrefixBothStringsStartWithSameCommonPrefixIsDetected()
        {
            // Non-null case.
            Assert.Equal(4, TextUtil.CommonPrefix("1234abcdef", "1234xyz"));
        }
        [Fact]
        public void CommonPrefixBothStringsStartWithSameCommonPrefixIsDetected2()
        {
            // Non-null case.
            Assert.Equal(4, TextUtil.CommonPrefix("abc1234abcdef", "efgh1234xyz", 3, 4));
        }

        [Fact]
        public void CommonPrefixFirstStringIsSubstringOfSecondCommonPrefixIsDetected()
        {

            // Whole case.
            Assert.Equal(4, TextUtil.CommonPrefix("1234", "1234xyz"));
        }

        [Fact]
        public void CommonSuffixDisjunctStringsNoCommonSuffix()
        {
            // Detect any common suffix.
            // Null case.
            Assert.Equal(0, TextUtil.CommonSuffix("abc", "xyz"));
        }

        [Fact]
        public void CommonSuffixBothStringsEndWithSameCommonSuffixIsDetected()
        {
            // Non-null case.
            Assert.Equal(4, TextUtil.CommonSuffix("abcdef1234", "xyz1234"));
        }
        [Fact]
        public void CommonSuffixBothStringsEndWithSameCommonSuffixIsDetected2()
        {
            // Non-null case.
            Assert.Equal(4, TextUtil.CommonSuffix("abcdef1234abcd", "xyz1234efgh", 10, 7));
        }

        [Fact]
        public void CommonSuffixFirstStringIsSubstringOfSecondCommonSuffixIsDetected()
        {
            // Whole case.
            Assert.Equal(4, TextUtil.CommonSuffix("1234", "xyz1234"));
        }
    }
}