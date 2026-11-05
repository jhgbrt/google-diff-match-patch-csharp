using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Orig = DiffMatchPatch.Original;

namespace DiffMatchPatch.Tests
{
    /// <summary>
    /// Randomized tests comparing this implementation against the original
    /// google diff-match-patch port (DiffMatchPatch.Original.cs).
    /// </summary>
    public class DifferentialTests
    {
        const int Iterations = 300;

        class OriginalBitap : Orig.diff_match_patch
        {
            public int Bitap(string text, string pattern, int loc) => match_bitap(text, pattern, loc);
        }

        static Orig.diff_match_patch NewOriginal() => new() { Diff_Timeout = 0 };

        static string RandomText(Random rnd, int maxLength, string alphabet)
        {
            var length = rnd.Next(maxLength + 1);
            var sb = new StringBuilder(length);
            for (var i = 0; i < length; i++)
                sb.Append(alphabet[rnd.Next(alphabet.Length)]);
            return sb.ToString();
        }

        static string Mutate(Random rnd, string text, string alphabet)
        {
            var sb = new StringBuilder(text);
            var edits = rnd.Next(1, 8);
            for (var e = 0; e < edits; e++)
            {
                var pos = rnd.Next(sb.Length + 1);
                switch (rnd.Next(3))
                {
                    case 0:
                        sb.Insert(pos, RandomText(rnd, 6, alphabet));
                        break;
                    case 1 when sb.Length > 0:
                        var len = Math.Min(rnd.Next(1, 6), sb.Length - Math.Min(pos, sb.Length - 1));
                        sb.Remove(Math.Min(pos, sb.Length - 1), len);
                        break;
                    default:
                        if (sb.Length > 0)
                            sb[Math.Min(pos, sb.Length - 1)] = alphabet[rnd.Next(alphabet.Length)];
                        break;
                }
            }
            return sb.ToString();
        }

        internal static IEnumerable<(string, string)> Pairs(int seed, int maxLength, string alphabet)
        {
            var rnd = new Random(seed);
            for (var i = 0; i < Iterations; i++)
            {
                var a = RandomText(rnd, maxLength, alphabet);
                var b = rnd.Next(4) == 0 ? RandomText(rnd, maxLength, alphabet) : Mutate(rnd, a, alphabet);
                yield return (a, b);
            }
        }

        static List<(string, string)> Normalize(IEnumerable<Diff> diffs)
            => diffs.Select(d => (d.Operation.ToString().ToUpperInvariant(), d.Text)).ToList();

        static List<(string, string)> Normalize(IEnumerable<Orig.Diff> diffs)
            => diffs.Select(d => (d.operation.ToString().ToUpperInvariant(), d.text)).ToList();

        static void AssertValid(string a, string b, IEnumerable<Diff> diffs, bool noAdjacentDuplicates = true)
        {
            var list = diffs.ToList();
            Assert.Equal(a, list.Text1());
            Assert.Equal(b, list.Text2());
            Assert.DoesNotContain(list, d => d.IsEmpty);
            if (!noAdjacentDuplicates) return;
            for (var i = 1; i < list.Count; i++)
                Assert.False(list[i].Operation == list[i - 1].Operation, $"adjacent {list[i].Operation} diffs at {i}");
        }

        public static TheoryData<string, int> Alphabets => new()
        {
            { "ab", 40 },
            { "abc \n", 300 },
            { "abcdefghij\n", 2000 },
        };

        [Theory]
        [MemberData(nameof(Alphabets))]
        public void Compute_MatchesOriginal(string alphabet, int maxLength)
        {
            var dmp = NewOriginal();
            foreach (var (a, b) in Pairs(alphabet.Length * 31 + maxLength, maxLength, alphabet))
            {
                foreach (var checklines in new[] { false, true })
                {
                    var actual = Diff.Compute(a, b, 0f, checklines);
                    AssertValid(a, b, actual);
                    // In line mode the original port can emit empty diffs, so only compare char mode exactly.
                    if (!checklines)
                        Assert.Equal(Normalize(dmp.diff_main(a, b, false)), Normalize(actual));
                }
            }
        }

        [Theory]
        [MemberData(nameof(Alphabets))]
        public void ComputeWithHalfMatch_MatchesOriginal(string alphabet, int maxLength)
        {
            // A (large) timeout enables the half-match speedup in both implementations.
            var dmp = new Orig.diff_match_patch { Diff_Timeout = 1000f };
            foreach (var (a, b) in Pairs(alphabet.Length * 13 + maxLength, maxLength, alphabet))
            {
                foreach (var checklines in new[] { false, true })
                {
                    var actual = Diff.Compute(a, b, checklines, true, System.Threading.CancellationToken.None);
                    AssertValid(a, b, actual);
                    if (!checklines)
                        Assert.Equal(Normalize(dmp.diff_main(a, b, false)), Normalize(actual));
                }
            }
        }

        [Theory]
        [MemberData(nameof(Alphabets))]
        public void Cleanup_MatchesOriginal(string alphabet, int maxLength)
        {
            var dmp = NewOriginal();
            foreach (var (a, b) in Pairs(alphabet.Length * 17 + maxLength, maxLength, alphabet))
            {
                var semantic = dmp.diff_main(a, b, false);
                dmp.diff_cleanupSemantic(semantic);
                var humanReadable = Diff.Compute(a, b, 0f, false).MakeHumanReadable();
                // semantic cleanup may legitimately leave adjacent edits of the same kind (as does the original)
                AssertValid(a, b, humanReadable, noAdjacentDuplicates: false);
                Assert.Equal(Normalize(semantic), Normalize(humanReadable));

                var efficiency = dmp.diff_main(a, b, false);
                dmp.diff_cleanupEfficiency(efficiency);
                var actual = Diff.Compute(a, b, 0f, false).OptimizeForMachineProcessing();
                AssertValid(a, b, actual, noAdjacentDuplicates: false);
                Assert.Equal(Normalize(efficiency), Normalize(actual));
                Assert.Equal(dmp.diff_toDelta(efficiency), actual.ToDelta());
            }
        }

        [Theory]
        [MemberData(nameof(Alphabets))]
        public void Patch_MatchesOriginal(string alphabet, int maxLength)
        {
            var dmp = NewOriginal();
            var rnd = new Random(42);
            foreach (var (a, b) in Pairs(alphabet.Length * 7 + maxLength, maxLength, alphabet))
            {
                // Patch.Compute must produce patches that turn a into b.
                var patches = Patch.Compute(a, b);
                var (roundTrip, roundTripResults) = patches.Apply(a);
                Assert.Equal(b, roundTrip);
                Assert.All(roundTripResults, Assert.True);

                // Patch construction from the same diffs must match the original exactly.
                // (Diffs are not compared because the original can emit empty diffs in line mode.)
                var diffs = Diff.Compute(a, b, 0f, true).MakeHumanReadable().OptimizeForMachineProcessing();
                var originalDiffs = diffs
                    .Select(d => new Orig.Diff((Orig.Operation)Enum.Parse(typeof(Orig.Operation), d.Operation.ToString().ToUpperInvariant()), d.Text))
                    .ToList();
                var expectedPatches = dmp.patch_make(a, originalDiffs);
                var patchText = dmp.patch_toText(expectedPatches);
                Assert.Equal(patchText, Patch.Compute(a, diffs).ToText());

                // Apply the same patch text to a slightly different text to exercise fuzzy matching (bitap).
                var target = Mutate(rnd, a, alphabet);
                var expected = dmp.patch_apply(dmp.patch_fromText(patchText), target);
                var (actualText, actualResults) = PatchList.Parse(patchText).Apply(target);
                Assert.Equal((string)expected[0], actualText);
                Assert.Equal((bool[])expected[1], actualResults);
            }
        }

        [Theory]
        [InlineData("ab", 60)]
        [InlineData("abcdef", 500)]
        [InlineData("abcdefghijklmnopqrstuvwxyz ", 3000)]
        public void Bitap_MatchesOriginal(string alphabet, int maxLength)
        {
            var original = new OriginalBitap();
            var bitap = new BitapAlgorithm(new MatchSettings(original.Match_Threshold, original.Match_Distance));
            var rnd = new Random(alphabet.Length + maxLength);
            for (var i = 0; i < Iterations; i++)
            {
                var text = RandomText(rnd, maxLength, alphabet);
                if (text.Length == 0) continue;
                var start = rnd.Next(text.Length);
                var length = Math.Min(rnd.Next(1, Constants.MatchMaxBits + 1), text.Length - start);
                var pattern = rnd.Next(2) == 0
                    ? Mutate(rnd, text.Substring(start, length), alphabet)
                    : RandomText(rnd, Constants.MatchMaxBits, alphabet);
                if (pattern.Length == 0 || pattern.Length > Constants.MatchMaxBits) continue;
                var loc = Math.Max(0, Math.Min(text.Length, start + rnd.Next(-20, 20)));
                Assert.Equal(original.Bitap(text, pattern, loc), bitap.Match(text, pattern, loc));
            }
        }

        [Fact]
        public void CleanupSemantic_DoesNotDuplicateText()
        {
            // CleanupSemanticLossless used to emit the shifted second equality and then the original one again.
            const string a = "babaaabb", b = "bbabbbaab";
            var diffs = Diff.Compute(a, b, 0f, false).MakeHumanReadable();
            Assert.Equal(a, diffs.Text1());
            Assert.Equal(b, diffs.Text2());
        }

        [Fact]
        public void PatchCompute_KeepsSmallEqualityThatMatchesLastDiff()
        {
            // A small equality with the same text as the final diff used to be mistaken for the last diff and dropped.
            var diffs = new[] { Diff.Equal("x"), Diff.Delete("a"), Diff.Equal("b"), Diff.Insert("c"), Diff.Equal("b") };
            var patches = Patch.Compute("xabb", diffs).ToList();
            var (text, results) = patches.Apply("xabb");
            Assert.Equal("xbcb", text);
            Assert.All(results, Assert.True);
            Assert.Equal("@@ -1,4 +1,4 @@\n x\n-a\n b\n+c\n b\n", patches.ToText());
        }
    }
}
