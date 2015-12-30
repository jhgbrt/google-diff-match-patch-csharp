using System;
using System.Collections.Generic;
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
            t.TestPerformance();
        }

        [TestMethod]
        [Ignore]
        public void TestPerformance()
        {
            var oldText = File.ReadAllText("left.txt");
            var newText = File.ReadAllText("right.txt");
            List<Diff> diff;
            diff = Diff.Compute(oldText, newText, 5);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1; i++)
            {
                diff = Diff.Compute(oldText, newText, 5);
            }
            Console.WriteLine(sw.ElapsedMilliseconds);
            //var fileName = Path.ChangeExtension(Path.GetTempFileName(), "html");
            //File.WriteAllText(fileName, diff.PrettyHtml());
            //Process.Start(fileName);
        }
    }
}
