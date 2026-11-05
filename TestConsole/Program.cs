using DiffMatchPatch;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
            if (args.Length > 0 && args[0] == "--profile")
            {
                RunProfile();
                return;
            }

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

        static void RunProfile()
        {
            var baseDir = AppContext.BaseDirectory;
            // Navigate up from bin/Release/net10.0 to the repo root, then into DiffMatchPatch.PerformanceTest
            var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
            string text1 = File.ReadAllText(Path.Combine(repoRoot, "DiffMatchPatch.PerformanceTest", "Speedtest1.txt"));
            string text2 = File.ReadAllText(Path.Combine(repoRoot, "DiffMatchPatch.PerformanceTest", "Speedtest2.txt"));

            // Warmup
            var warmup = Diff.Compute(text1, text2, 5);
            warmup.MakeHumanReadable();
            warmup.OptimizeForMachineProcessing();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Actual profiled run - 10 iterations to amplify signal
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10; i++)
            {
                var diff = Diff.Compute(text1, text2, 5);
                diff.MakeHumanReadable();
                diff.OptimizeForMachineProcessing();
            }
            long after = GC.GetAllocatedBytesForCurrentThread();

            Console.WriteLine($"Allocated {(after - before) / 1024.0 / 1024.0:F2} MB across 10 iterations ({(after - before) / 10 / 1024.0 / 1024.0:F2} MB/iteration)");
        }

        readonly record struct MyRecord(ImmutableList<int> SomeList);
    }
}
