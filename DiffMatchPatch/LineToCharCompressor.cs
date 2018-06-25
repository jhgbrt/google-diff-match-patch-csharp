using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiffMatchPatch
{
    class LineToCharCompressor
    {
        /// <summary>
        /// Compresses all lines of a text to a series of indexes (starting at \u0001, ending at (char)text.Length)
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public string Compress(string text) 
            => EnsureHashed(text.SplitLines()).Aggregate(new StringBuilder(), (sb, line) => sb.Append(this[line])).ToString();

        /// <summary>
        /// Decompresses a series of characters that was previously compressed back to the original lines of text.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public string Decompress(string text) 
            => text.Aggregate(new StringBuilder(), (sb, c) => sb.Append(this[c])).ToString();

        // e.g. _lineArray[4] == "Hello\n"
        // e.g. _lineHash["Hello\n"] == 4
        readonly List<string> _lineArray = new List<string>();
        readonly Dictionary<string, char> _lineHash = new Dictionary<string, char>();

        IEnumerable<string> EnsureHashed(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (!_lineHash.ContainsKey(line))
                {
                    _lineArray.Add(line);
                    // "\u0000" is a valid character, but various debuggers don't like it. 
                    // Therefore, add Count, not Count - 1
                    _lineHash.Add(line, (char)_lineArray.Count);
                }
                yield return line;
            }
        }

        char this[string line] => _lineHash[line];
        string this[int c] => _lineArray[c - 1];

    }
}