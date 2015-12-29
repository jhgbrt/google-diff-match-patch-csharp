using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class TextUtil_LinesToCharsTests
    {
        [TestMethod]
        public void LinesToCharsWhenCalledConvertsLinesToChars()
        {
            var tmpVector = new List<string> { "", "alpha\n", "beta\n" };
            var result = TextUtil.LinesToChars("alpha\nbeta\nalpha\n", "beta\nalpha\nbeta\n");
            Assert.AreEqual("\u0001\u0002\u0001", result.Item1);
            Assert.AreEqual("\u0002\u0001\u0002", result.Item2);
            CollectionAssert.AreEqual(tmpVector, result.Item3);
        }

        [TestMethod]
        public void LinesToCharsWhenCalledEmptyText1ConvertsLinesToChars()
        {
            var tmpVector = new List<string> {"", "alpha\r\n", "beta\r\n", "\r\n"};
            var result = TextUtil.LinesToChars("", "alpha\r\nbeta\r\n\r\n\r\n");
            Assert.AreEqual("", result.Item1);
            Assert.AreEqual("\u0001\u0002\u0003\u0003", result.Item2);
            CollectionAssert.AreEqual(tmpVector, result.Item3);
        }

        [TestMethod]
        public void LinesToCharsDisjunctSet()
        {
            var tmpVector = new List<string> {"", "a", "b"};
            var result = TextUtil.LinesToChars("a", "b");
            Assert.AreEqual("\u0001", result.Item1);
            Assert.AreEqual("\u0002", result.Item2);
            CollectionAssert.AreEqual(tmpVector, result.Item3);
        }

        [TestMethod]
        public void LinesToCharsMoreThan300Entries()
        {
            var tmpVector = new List<string>();
            // More than 256 to reveal any 8-bit limitations.
            var n = 300;
            var lineList = new StringBuilder();
            var charList = new StringBuilder();
            for (var x = 1; x < n + 1; x++)
            {
                tmpVector.Add(x + "\n");
                lineList.Append(x + "\n");
                charList.Append(Convert.ToChar(x));
            }
            Assert.AreEqual(n, tmpVector.Count);
            var lines = lineList.ToString();
            var chars = charList.ToString();
            Assert.AreEqual(n, chars.Length);
            tmpVector.Insert(0, "");
            var result = TextUtil.LinesToChars(lines, "");
            Assert.AreEqual(chars, result.Item1);
            Assert.AreEqual("", result.Item2);
            CollectionAssert.AreEqual(tmpVector, result.Item3);
        }


    }
}