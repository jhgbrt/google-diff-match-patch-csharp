using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiffMatchPatch.PerformanceTest
{
    [TestClass]
    public class PerformanceTest
    {
        public static void Main()
        {
            var t = new PerformanceTest();
            t.TestPerformance1();
        }

        [TestMethod]
        public void TestPerformance1()
        {
            var oldText = File.ReadAllText("left.txt");
            var newText = File.ReadAllText("right.txt");

     
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1; i++)
            {
                var diff = Diff.Compute(oldText, newText, 5);
                diff.CleanupEfficiency();
                diff.CleanupSemantic();
            }
            //var patched = Patch.FromDiffs(diff).Apply(oldText);
            var elapsed = sw.Elapsed;
            Console.WriteLine(elapsed);
            //var fileName = Path.ChangeExtension(Path.GetTempFileName(), "html");
            //File.WriteAllText(fileName, diff.PrettyHtml());
            //Process.Start(fileName);
        }

        [TestMethod]
        public void TestPerformance2()
        {
            string text1 = File.ReadAllText("Speedtest1.txt");
            string text2 = File.ReadAllText("Speedtest2.txt");


            // Execute one reverse diff as a warmup.
            Diff.Compute(text2, text1);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var sw = Stopwatch.StartNew();
            var diff = Diff.Compute(text1, text2);
            Console.WriteLine("Elapsed time: " + sw.Elapsed);
            //var fileName = Path.ChangeExtension(Path.GetTempFileName(), "html");
            //File.WriteAllText(fileName, diff.PrettyHtml());
            //Process.Start(fileName);
        }
    }
}
