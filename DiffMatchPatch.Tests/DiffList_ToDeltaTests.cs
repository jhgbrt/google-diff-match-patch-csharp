using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace DiffMatchPatch.Tests
{

    public class DiffList_ToDeltaTests
    {
        IReadOnlyCollection<Diff> diffs = new List<Diff>
        {
            Diff.Equal("jump"),
            Diff.Delete("s"),
            Diff.Insert("ed"),
            Diff.Equal(" over "),
            Diff.Delete("the"),
            Diff.Insert("a"),
            Diff.Equal(" lazy"),
            Diff.Insert("old dog")
        };

        [Fact]
        public void Verify()
        {
            var text1 = diffs.Text1();
            Assert.Equal("jumps over the lazy", text1);
            
        }

        [Fact]
        public void ToDelta_GeneratesExpectedOutput()
        {
            var delta = diffs.ToDelta();
            Assert.Equal("=4\t-1\t+ed\t=6\t-3\t+a\t=5\t+old dog", delta);
        }
        [Fact]
        public void FromDelta_EmptyTokensAreOk()
        {
            var delta = "\t\t";
            var diffs = DiffList.FromDelta("", delta);
            Assert.Empty(diffs);
        }

        [Fact]
        public void FromDelta_GeneratesExpectedDiffs()
        {
            var delta = diffs.ToDelta();
            var result  = DiffList.FromDelta(diffs.Text1(), delta);
            Assert.Equal(diffs, result.ToList());
            
        }
        [Fact]
        public void FromDelta_InputTooLong_Throws()
        {
            var delta = diffs.ToDelta();
            var text1 = diffs.Text1() + "x";
            Assert.Throws<ArgumentException>(() =>
                DiffList.FromDelta(text1, delta).ToList()
            );
        }

        [Fact]
        public void FromDelta_InvalidInput_Throws()
        {
            var delta = "=x";
            Assert.Throws<ArgumentException>(() => 
                DiffList.FromDelta("", delta).ToList()
            );
        }
        [Fact]
        public void ToDelta_InputTooShort_Throws()
        {
            var delta = diffs.ToDelta();
            var text1 = diffs.Text1()[1..];
            Assert.Throws<ArgumentException>(() =>
                DiffList.FromDelta(text1, delta).ToList()
            );
        }

        [Fact]
        public void Delta_SpecialCharacters_Works()
        {
            var zero = (char)0;
            var one = (char)1;
            var two = (char)2;
            diffs = new List<Diff>
            {
                Diff.Equal("\u0680 " + zero + " \t %"),
                Diff.Delete("\u0681 " + one + " \n ^"),
                Diff.Insert("\u0682 " + two + " \\ |")
            };
            var text1 = diffs.Text1();
            Assert.Equal("\u0680 " + zero + " \t %\u0681 " + one + " \n ^", text1);

            var delta = diffs.ToDelta();
            // Lowercase, due to UrlEncode uses lower.
            Assert.Equal("=7\t-7\t+%da%82 %02 %5c %7c", delta);

            Assert.Equal(diffs, DiffList.FromDelta(text1, delta).ToList());
        }


        [Fact]
        public void Delta_FromUnchangedCharacters_Succeeds()
        {
            // Verify pool of unchanged characters.
            var expected = new List<Diff>
            {
                Diff.Insert("A-Z a-z 0-9 - _ . ! ~ * ' ( ) ; / ? : @ & = + $ , # ")
            };
            var text2 = expected.Text2();
            Assert.Equal("A-Z a-z 0-9 - _ . ! ~ * \' ( ) ; / ? : @ & = + $ , # ", text2);

            var delta = expected.ToDelta();
            Assert.Equal("+A-Z a-z 0-9 - _ . ! ~ * \' ( ) ; / ? : @ & = + $ , # ", delta);

            // Convert delta string into a diff.
            var actual = DiffList.FromDelta("", delta);
            Assert.Equal(expected, actual.ToList());
        }

        [Fact]
        public void Delta_LargeString()
        {

            // 160 kb string.
            string a = "abcdefghij";
            for (int i = 0; i < 14; i++)
            {
                a += a;
            }
            var diffs2 = new List<Diff> { Diff.Insert(a) };
            var delta = diffs2.ToDelta();
            Assert.Equal("+" + a, delta);

            // Convert delta string into a diff.
            Assert.Equal(diffs2, DiffList.FromDelta("", delta).ToList());

        }
    }
}