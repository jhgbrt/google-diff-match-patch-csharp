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
        public void Compress_ConvertsLinesToChars()
        {
            var compressor = new LineToCharCompressor();
            var result1 = compressor.Compress("alpha\nbeta\nalpha\n");
            var result2 = compressor.Compress("beta\nalpha\nbeta\n");
            Assert.AreEqual("\u0001\u0002\u0001", result1);
            Assert.AreEqual("\u0002\u0001\u0002", result2);
        }

        [TestMethod]
        public void Compress_WhenCalled_EmptyText_ReturnsEmptyString()
        {
            var compressor = new LineToCharCompressor();
            var result = compressor.Compress(string.Empty);
            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void Compress_OneLine()
        {
            var d = new LineToCharCompressor();
            var result = d.Compress("a");
            Assert.AreEqual("\u0001", result);
        }
        
        [TestMethod]
        public void Compress_MultipleLines()
        {
            var d = new LineToCharCompressor();
            var result = d.Compress("line1\r\nline2\r\n");
            Assert.AreEqual("\u0001\u0002", result);
        }

        [TestMethod]
        public void Decompress()
        {
            var d = new LineToCharCompressor();
            var input = "line1\r\nline2\r\n";
            var compressed = d.Compress(input);
            var result = d.Decompress(compressed);
            Assert.AreEqual(input, result);
        }

        [TestMethod]
        public void Decompress_OneLine()
        {
            var compressor = new LineToCharCompressor();
            var result = compressor.Compress("a");
            Assert.AreEqual("\u0001", result);
        }

        [TestMethod]
        public void Compress_DisjunctStrings()
        {
            var compressor = new LineToCharCompressor();
            var result1 = compressor.Compress("a");
            var result2 = compressor.Compress("b");

            Assert.AreEqual("\u0001", result1);
            Assert.AreEqual("\u0002", result2);
        }

        [TestMethod]
        public void Compress_MoreThan300Entries()
        {
            // More than 256 to reveal any 8-bit limitations.
            var n = 300;
            var lineList = new StringBuilder();
            var charList = new StringBuilder();
            for (var x = 1; x < n + 1; x++)
            {
                lineList.Append(x + "\n");
                charList.Append(Convert.ToChar(x));
            }

            var lines = lineList.ToString();
            var chars = charList.ToString();
            Assert.AreEqual(n, chars.Length);

            var compressor = new LineToCharCompressor();
            var result = compressor.Compress(lines);

            Assert.AreEqual(chars, result);
        }


    }
}