using System;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace DiffMatchPatch.Tests
{
    public class TextUtil_UrlEncodeTests
    {
        // The previous implementation (Uri.EscapeDataString + walk back some escapes + lowercase hex), used as oracle.
        static string ReferenceEncode(string str)
        {
            var sb = new StringBuilder(Uri.EscapeDataString(str));
            sb = sb.Replace('+', ' ').Replace("%20", " ").Replace("%21", "!")
                .Replace("%2A", "*").Replace("%27", "'").Replace("%28", "(")
                .Replace("%29", ")").Replace("%3B", ";").Replace("%2F", "/")
                .Replace("%3F", "?").Replace("%3A", ":").Replace("%40", "@")
                .Replace("%26", "&").Replace("%3D", "=").Replace("%2B", "+")
                .Replace("%24", "$").Replace("%2C", ",").Replace("%23", "#");
            return Regex.Replace(sb.ToString(), "%[0-9A-F][0-9A-F]", m => m.Value.ToLower());
        }

        [Fact]
        public void UrlEncoded_MatchesReferenceImplementation()
        {
            const string alphabet = "aZ09-_.~ !*'();/?:@&=+$,#%\t\n\r\"<>[]{}|\\^`é€中 ";
            var rnd = new Random(7);
            for (var i = 0; i < 2000; i++)
            {
                var sb = new StringBuilder();
                var length = rnd.Next(30);
                for (var j = 0; j < length; j++)
                {
                    switch (rnd.Next(10))
                    {
                        case 0: sb.Append("😀"); break; // surrogate pair
                        default: sb.Append(alphabet[rnd.Next(alphabet.Length)]); break;
                    }
                }
                var text = sb.ToString();
                Assert.Equal(ReferenceEncode(text), text.UrlEncoded());
            }
        }

        [Fact]
        public void UrlEncoded_AllAsciiCharacters_MatchReference()
        {
            for (var c = (char)0; c < 128; c++)
                Assert.Equal(ReferenceEncode(c.ToString()), c.ToString().UrlEncoded());
        }

        [Fact]
        public void UrlEncoded_LoneSurrogate_IsEncodedAsReplacementCharacter()
        {
            Assert.Equal("a%ef%bf%bdb", "a\ud83db".UrlEncoded());
            Assert.Equal("%ef%bf%bd", "\ude00".UrlEncoded());
        }

        [Fact]
        public void UrlEncoded_RoundTrips()
        {
            var text = "Hello, wörld! 中文 😀 100% = 50/50 #tag\n";
            Assert.Equal(text, text.UrlEncoded().UrlDecoded());
        }
    }
}
