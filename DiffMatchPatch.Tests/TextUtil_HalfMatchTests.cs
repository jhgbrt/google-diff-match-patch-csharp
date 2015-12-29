using DiffMatchPatch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nicTest
{

    public class TextUtilHalfMatchTests
    {
        [TestMethod]
        public void WhenTextDoesNotMatchReturnsNull()
        {
            // No match.
            var result = TextUtil.HalfMatch("1234567890", "abcdef");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void WhenSubstringIsLessThanHalfTheOriginalStringReturnsNull()
        {
            var result = TextUtil.HalfMatch("12345", "23");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void WhenSubstringIsMoreThanHalfTheOriginalStringReturnsResult1()
        {

            var result = TextUtil.HalfMatch("1234567890", "a345678z");
            Assert.AreEqual(new HalfMatchResult("12", "90", "a", "z", "345678"), result);
        }

        [TestMethod]
        public void WhenSubstringIsMoreThanHalfTheOriginalStringReturnsResult2()
        {
            var result = TextUtil.HalfMatch("a345678z", "1234567890");
            Assert.AreEqual(new HalfMatchResult("a", "z", "12", "90", "345678"), result);
        }

        [TestMethod]
        public void WhenSubstringIsMoreThanHalfTheOriginalStringReturnsResult3()
        {
            var result = TextUtil.HalfMatch("abc56789z", "1234567890");
            Assert.AreEqual(new HalfMatchResult("abc", "z", "1234", "0", "56789"), result);

        }

        [TestMethod]
        public void WhenSubstringIsMoreThanHalfTheOriginalStringReturnsResult4()
        {
            var result = TextUtil.HalfMatch("a23456xyz", "1234567890");
            Assert.AreEqual(new HalfMatchResult("a", "xyz", "1", "7890", "23456"), result);
        }

        [TestMethod]
        public void WhenSubstringIsMoreThanHalfTheOriginalStringMultipleMatchesReturnsLongestSubstring1()
        {

            var result = TextUtil.HalfMatch("121231234123451234123121", "a1234123451234z");
            Assert.AreEqual(new HalfMatchResult("12123", "123121", "a", "z", "1234123451234"), result);

        }

        [TestMethod]
        public void WhenSubstringIsMoreThanHalfTheOriginalStringMultipleMatchesReturnsLongestSubstring2()
        {
            var result = TextUtil.HalfMatch("x-=-=-=-=-=-=-=-=-=-=-=-=", "xx-=-=-=-=-=-=-=");
            Assert.AreEqual(new HalfMatchResult("", "-=-=-=-=-=", "x", "", "x-=-=-=-=-=-=-="), result);
        }

        [TestMethod]
        public void WhenSubstringIsMoreThanHalfTheOriginalStringMultipleMatchesReturnsLongestSubstring3()
        {
 
            var result = TextUtil.HalfMatch("-=-=-=-=-=-=-=-=-=-=-=-=y", "-=-=-=-=-=-=-=yy");
            Assert.AreEqual(new HalfMatchResult("-=-=-=-=-=", "", "", "y", "-=-=-=-=-=-=-=y"), result);
       }

        [TestMethod]
        public void WhenSubstringIsMoreThanHalfTheOriginalStringNonOptimal()
        {
            // Non-optimal halfmatch.
            // Optimal diff would be -q+x=H-i+e=lloHe+Hu=llo-Hew+y not -qHillo+x=HelloHe-w+Hulloy
            var result = TextUtil.HalfMatch("qHilloHelloHew", "xHelloHeHulloy");
            Assert.AreEqual(new HalfMatchResult("qHillo", "w", "x", "Hulloy", "HelloHe"), result);
        }
    }
}