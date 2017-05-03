using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class TextUtilHalfMatchTests
    {
        [TestMethod]
        public void WhenLeftIsEmptyReturnsEmpty()
        {
            var result = TextUtil.HalfMatch("", "12345");
            Assert.IsTrue(result.IsEmpty);
        }

        [TestMethod]
        public void WhenRightIsEmptyReturnsEmpty()
        {
            var result = TextUtil.HalfMatch("12345", "");
            Assert.IsTrue(result.IsEmpty);
        }

        [TestMethod]
        public void WhenTextDoesNotMatchReturnsNull()
        {
            // No match.
            var result = TextUtil.HalfMatch("1234567890", "abcdef");
            Assert.IsTrue(result.IsEmpty);
        }

        [TestMethod]
        public void WhenSubstringIsLessThanHalfTheOriginalStringReturnsNull()
        {
            var result = TextUtil.HalfMatch("12345", "23");
            Assert.IsTrue(result.IsEmpty);
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

        [TestMethod]
        public void diff_halfmatchTest()
        {
            // No match.
            CollectionAssert.AreEqual(new string[] { "a", "z", "12", "90", "345678" }, diff_halfMatch("a345678z", "1234567890"));
            return;

            Assert.IsNull(diff_halfMatch("1234567890", "abcdef"));

            Assert.IsNull(diff_halfMatch("12345", "23"));

            // Single Match.
            CollectionAssert.AreEqual(new string[] { "12", "90", "a", "z", "345678" }, diff_halfMatch("1234567890", "a345678z"));


            CollectionAssert.AreEqual(new string[] { "abc", "z", "1234", "0", "56789" }, diff_halfMatch("abc56789z", "1234567890"));

            CollectionAssert.AreEqual(new string[] { "a", "xyz", "1", "7890", "23456" }, diff_halfMatch("a23456xyz", "1234567890"));

            // Multiple Matches.
            CollectionAssert.AreEqual(new string[] { "12123", "123121", "a", "z", "1234123451234" }, diff_halfMatch("121231234123451234123121", "a1234123451234z"));

            CollectionAssert.AreEqual(new string[] { "", "-=-=-=-=-=", "x", "", "x-=-=-=-=-=-=-=" }, diff_halfMatch("x-=-=-=-=-=-=-=-=-=-=-=-=", "xx-=-=-=-=-=-=-="));

            CollectionAssert.AreEqual(new string[] { "-=-=-=-=-=", "", "", "y", "-=-=-=-=-=-=-=y" }, diff_halfMatch("-=-=-=-=-=-=-=-=-=-=-=-=y", "-=-=-=-=-=-=-=yy"));

            // Non-optimal halfmatch.
            // Optimal diff would be -q+x=H-i+e=lloHe+Hu=llo-Hew+y not -qHillo+x=HelloHe-w+Hulloy
            CollectionAssert.AreEqual(new string[] { "qHillo", "w", "x", "Hulloy", "HelloHe" }, diff_halfMatch("qHilloHelloHew", "xHelloHeHulloy"));

        }


        protected string[] diff_halfMatch(string text1, string text2)
        {
            string longtext = text1.Length > text2.Length ? text1 : text2;
            string shorttext = text1.Length > text2.Length ? text2 : text1;
            if (longtext.Length < 4 || shorttext.Length * 2 < longtext.Length)
            {
                return null;  // Pointless.
            }

            // First check if the second quarter is the seed for a half-match.
            string[] hm1 = diff_halfMatchI(longtext, shorttext,
                                           (longtext.Length + 3) / 4);
            // Check again based on the third quarter.
            string[] hm2 = diff_halfMatchI(longtext, shorttext,
                                           (longtext.Length + 1) / 2);
            string[] hm;
            if (hm1 == null && hm2 == null)
            {
                return null;
            }
            else if (hm2 == null)
            {
                hm = hm1;
            }
            else if (hm1 == null)
            {
                hm = hm2;
            }
            else
            {
                // Both matched.  Select the longest.
                hm = hm1[4].Length > hm2[4].Length ? hm1 : hm2;
            }

            // A half-match was found, sort out the return data.
            if (text1.Length > text2.Length)
            {
                return hm;
                //return new string[]{hm[0], hm[1], hm[2], hm[3], hm[4]};
            }
            else
            {
                return new string[] { hm[2], hm[3], hm[0], hm[1], hm[4] };
            }
        }

        /**
         * Does a Substring of shorttext exist within longtext such that the
         * Substring is at least half the length of longtext?
         * @param longtext Longer string.
         * @param shorttext Shorter string.
         * @param i Start index of quarter length Substring within longtext.
         * @return Five element string array, containing the prefix of longtext, the
         *     suffix of longtext, the prefix of shorttext, the suffix of shorttext
         *     and the common middle.  Or null if there was no match.
         */
        private string[] diff_halfMatchI(string longtext, string shorttext, int i)
        {
            // Start with a 1/4 length Substring at position i as a seed.
            string seed = longtext.Substring(i, longtext.Length / 4);
            int j = -1;
            string best_common = string.Empty;
            string best_longtext_a = string.Empty, best_longtext_b = string.Empty;
            string best_shorttext_a = string.Empty, best_shorttext_b = string.Empty;
            while (j < shorttext.Length && (j = shorttext.IndexOf(seed, j + 1,
                StringComparison.Ordinal)) != -1)
            {
                int prefixLength = diff_commonPrefix(longtext.Substring(i),
                                                     shorttext.Substring(j));
                int suffixLength = diff_commonSuffix(longtext.Substring(0, i),
                                                     shorttext.Substring(0, j));
                if (best_common.Length < suffixLength + prefixLength)
                {
                    best_common = shorttext.Substring(j - suffixLength, suffixLength)
                        + shorttext.Substring(j, prefixLength);
                    best_longtext_a = longtext.Substring(0, i - suffixLength);
                    best_longtext_b = longtext.Substring(i + prefixLength);
                    best_shorttext_a = shorttext.Substring(0, j - suffixLength);
                    best_shorttext_b = shorttext.Substring(j + prefixLength);
                }
            }
            if (best_common.Length * 2 >= longtext.Length)
            {
                return new string[]{best_longtext_a, best_longtext_b,best_shorttext_a, best_shorttext_b, best_common};
            }
            else
            {
                return null;
            }
        }
        public int diff_commonSuffix(string text1, string text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            int text1_length = text1.Length;
            int text2_length = text2.Length;
            int n = Math.Min(text1.Length, text2.Length);
            for (int i = 1; i <= n; i++)
            {
                if (text1[text1_length - i] != text2[text2_length - i])
                {
                    return i - 1;
                }
            }
            return n;
        }
        public int diff_commonPrefix(string text1, string text2)
        {
            // Performance analysis: http://neil.fraser.name/news/2007/10/09/
            int n = Math.Min(text1.Length, text2.Length);
            for (int i = 0; i < n; i++)
            {
                if (text1[i] != text2[i])
                {
                    return i;
                }
            }
            return n;
        }
    }
}