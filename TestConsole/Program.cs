using DiffMatchPatch;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}
namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var diffs = new List<Diff>
            {
                Diff.Insert(" "),
                Diff.Equal("a"),
                Diff.Insert("nd"),
                Diff.Equal(" [[Pennsylvania]]"),
                Diff.Delete(" and [[New")
            };

            var patch1 = Patch.FromDiffs(diffs);

            Console.WriteLine(diffs.Text1());
            Console.WriteLine(diffs.Text2());

            var patch2 = Patch.Compute(diffs.Text1(), diffs.Text2(), 0, 4);

            Console.WriteLine(patch1.ToText());
            Console.WriteLine(patch2.ToText());

            //Debug.Assert(patch1.SequenceEqual(patch2));

            ImmutableList<int> someList = Enumerable.Range(0, 10).ToImmutableList();
            var record1 = new MyRecord(someList);
            var record2 = new MyRecord(someList);

            Console.WriteLine(record1 == record2);
        }

        readonly record struct MyRecord(ImmutableList<int> SomeList);
    }
}
