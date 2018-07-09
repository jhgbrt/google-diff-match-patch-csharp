using System;
using System.Collections.Generic;
using System.Linq;
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
            Assert.AreEqual("\u0000\u0001\u0000", result1);
            Assert.AreEqual("\u0001\u0000\u0001", result2);
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
            Assert.AreEqual("\u0000", result);
        }
        
        [TestMethod]
        public void Compress_MultipleLines()
        {
            var d = new LineToCharCompressor();
            var result = d.Compress("line1\r\nline2\r\n");
            Assert.AreEqual("\u0000\u0001", result);
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
            Assert.AreEqual("\u0000", result);
        }

        [TestMethod]
        public void Compress_DisjunctStrings()
        {
            var compressor = new LineToCharCompressor();
            var result1 = compressor.Compress("a");
            var result2 = compressor.Compress("b");

            Assert.AreEqual("\u0000", result1);
            Assert.AreEqual("\u0001", result2);
        }

        [TestMethod]
        public void Compress_MoreThan300Entries()
        {
            // More than 256 to reveal any 8-bit limitations.
            var n = 300;
            var lineList = new StringBuilder();
            var charList = new StringBuilder();
            for (var x = 0; x < n; x++)
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

        [TestMethod]
        public void Compress_MoreThan65535Lines_DecompressesCorrectly()
        {
            // More than 65536 to verify any 16-bit limitation.
            var lineList = new StringBuilder();
            for (int i = 0; i < 66000; i++)
            {
                lineList.Append(i + "\n");
            }
            var chars = lineList.ToString();

            LineToCharCompressor compressor = new LineToCharCompressor();

            var result = compressor.Compress(chars, sizeof(char));
            var decompressed = compressor.Decompress(result);

            AssertEqual(chars, decompressed);
        }

        private static void AssertEqual(string expected, string result)
        {
            Assert.AreEqual(expected.Length, result.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i]!=result[i])
                    Assert.Fail($"strings differ at position {i}");
            }
        }

        [TestMethod]
        public void MultipleTexts()
        {
            var text1 = Enumerable.Range(1, 70000).Aggregate(new StringBuilder(), (sb, i) => sb.Append(i).AppendLine()).ToString();
            var text2 = Enumerable.Range(20000, 999999).Aggregate(new StringBuilder(), (sb, i) => sb.Append(i).AppendLine()).ToString();

            var compressor = new LineToCharCompressor();

            var compressed1 = compressor.Compress(text1, 40000);
            var compressed2 = compressor.Compress(text2);

            var decompressed1 = compressor.Decompress(compressed1);
            var decompressed2 = compressor.Decompress(compressed2);

            AssertEqual(text1, decompressed1);
            AssertEqual(text2, decompressed2);
        }
    }
}