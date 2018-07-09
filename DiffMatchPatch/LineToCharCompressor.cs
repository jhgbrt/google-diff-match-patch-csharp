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
        /// <param name="maxLines"></param>
        /// <returns></returns>
        public string Compress(string text, int maxLines = char.MaxValue) 
            => Encode(text, maxLines).Aggregate(new StringBuilder(), (sb, c) => sb.Append(c)).ToString();

        IEnumerable<char> Encode(string text, int maxLines)
        {
            var start = 0;
            var end = -1;
            while (end < text.Length - 1)
            {
                end = _lineArray.Count == maxLines ? text.Length - 1 : text.IndexOf('\n', start);
                if (end == -1)
                {
                    end = text.Length - 1;
                }
                var line = text.Substring(start, end + 1 - start);
                EnsureHashed(line);
                yield return this[line];
                start = end + 1;
            }
        }

        /// <summary>
        /// Decompresses a series of characters that was previously compressed back to the original lines of text.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public string Decompress(string text) 
            => text.Aggregate(new StringBuilder(), (sb, c) => sb.Append(this[c])).Append(text.Length == char.MaxValue ? this[char.MaxValue] : "").ToString();

        // e.g. _lineArray[4] == "Hello\n"
        // e.g. _lineHash["Hello\n"] == 4
        readonly List<string> _lineArray = new List<string>();
        readonly Dictionary<string, char> _lineHash = new Dictionary<string, char>();

        void EnsureHashed(string line)
        {
            if (_lineHash.ContainsKey(line)) return;
            _lineArray.Add(line);
            _lineHash.Add(line, (char) (_lineArray.Count - 1));
        }

        char this[string line] => _lineHash[line];
        string this[int c] => _lineArray[c];

    }
}