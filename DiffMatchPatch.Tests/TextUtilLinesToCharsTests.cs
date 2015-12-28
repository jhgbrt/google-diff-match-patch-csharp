using System;
using System.Collections.Generic;
using System.Text;
using DiffMatchPatch;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace nicTest
{
    [TestClass]
    public class TextUtil_LinesToCharsTests
    {
        [TestMethod]
        public void LinesToChars_WhenCalled_ConvertsLinesToChars()
        {
            List<string> tmpVector = new List<string> { "", "alpha\n", "beta\n" };
            var result = TextUtil.LinesToChars("alpha\nbeta\nalpha\n", "beta\nalpha\nbeta\n");
            Assert.AreEqual("\u0001\u0002\u0001", result.Item1);
            Assert.AreEqual("\u0002\u0001\u0002", result.Item2);
            CollectionAssert.AreEqual(tmpVector, result.Item3);
        }

        [TestMethod]
        public void LinesToChars_WhenCalled_EmptyText1_ConvertsLinesToChars()
        {
            List<string> tmpVector = new List<string> {"", "alpha\r\n", "beta\r\n", "\r\n"};
            var result = TextUtil.LinesToChars("", "alpha\r\nbeta\r\n\r\n\r\n");
            Assert.AreEqual("", result.Item1);
            Assert.AreEqual("\u0001\u0002\u0003\u0003", result.Item2);
            CollectionAssert.AreEqual(tmpVector, result.Item3);
        }

        [TestMethod]
        public void LinesToChars_DisjunctSet()
        {
            List<string> tmpVector = new List<string> {"", "a", "b"};
            var result = TextUtil.LinesToChars("a", "b");
            Assert.AreEqual("\u0001", result.Item1);
            Assert.AreEqual("\u0002", result.Item2);
            CollectionAssert.AreEqual(tmpVector, result.Item3);
        }

        [TestMethod]
        public void LinesToChars_MoreThan300Entries()
        {
            var tmpVector = new List<string>();
            // More than 256 to reveal any 8-bit limitations.
            int n = 300;
            StringBuilder lineList = new StringBuilder();
            StringBuilder charList = new StringBuilder();
            for (int x = 1; x < n + 1; x++)
            {
                tmpVector.Add(x + "\n");
                lineList.Append(x + "\n");
                charList.Append(Convert.ToChar(x));
            }
            Assert.AreEqual(n, tmpVector.Count);
            string lines = lineList.ToString();
            string chars = charList.ToString();
            Assert.AreEqual(n, chars.Length);
            tmpVector.Insert(0, "");
            var result = TextUtil.LinesToChars(lines, "");
            Assert.AreEqual(chars, result.Item1);
            Assert.AreEqual("", result.Item2);
            CollectionAssert.AreEqual(tmpVector, result.Item3);
        }


    }
}