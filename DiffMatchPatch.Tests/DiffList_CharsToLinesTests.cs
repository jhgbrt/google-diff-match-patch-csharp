using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.Tests
{
    [TestClass]
    public class DiffList_CharsToLinesTests
    {
        [TestMethod]
        public void CharsToLines_ValidCharsWithCorrespondingLines_RestoresDiffsCorrectly()
        {
            // Convert chars up to lines.
            var diffs = new List<Diff>
            {
                Diff.Equal("\u0001\u0002\u0001"),
                Diff.Insert("\u0002\u0001\u0002")
            };
            var tmpVector = new List<string> {"", "alpha\n", "beta\n"};
            var expected = new List<Diff>
            {
                Diff.Equal("alpha\nbeta\nalpha\n"),
                Diff.Insert("beta\nalpha\nbeta\n")
            };
            var result = diffs.CharsToLines(tmpVector).ToList();
            CollectionAssert.AreEqual(expected, result);
        }

        [TestMethod]
        public void CharsToLines_MoreThan256Chars_RestoresDiffCorrectly()
        {

            // More than 256 to reveal any 8-bit limitations.
            var n = 300;
            var tmpVector = new List<string>();
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
            var diffs = new []
            {
                Diff.Delete(chars)
            };
            
            var result = diffs.CharsToLines(tmpVector).ToList();
            
            var expected = new List<Diff>
            {
                Diff.Delete(lines)
            };
            CollectionAssert.AreEqual(expected, result);
        }
    }
    [TestClass]
    public class LineToCharCompressorTests
    {
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

            var result = compressor.Compress(chars);
            var decompressed = compressor.Decompress(result);

            Assert.AreEqual(chars.Length, decompressed.Length);
            for (int i = 0; i < chars.Length; i++)
            {
                Assert.AreEqual(chars[i], decompressed[i]);
            }

        }

    }
}