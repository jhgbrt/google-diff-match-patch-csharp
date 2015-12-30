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
        [TestMethod]
        public void TestPerformance()
        {
            var oldText = File.ReadAllText("left.txt");
            var newText = File.ReadAllText("right.txt");
            List<Diff> diff;
            diff = Diff.Compute(oldText, newText, 5);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                diff = Diff.Compute(oldText, newText);
            }
            Console.WriteLine(sw.ElapsedMilliseconds);
            //var fileName = Path.ChangeExtension(Path.GetTempFileName(), "html");
            //File.WriteAllText(fileName, diff.PrettyHtml());
            //Process.Start(fileName);
        }
    }
}
