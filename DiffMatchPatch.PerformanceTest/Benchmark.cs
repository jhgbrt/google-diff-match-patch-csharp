using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace DiffMatchPatch.Benchmark
{
    [MemoryDiagnoser]
    public class DiffBenchmark
    {
        private string _text1 = null!;
        private string _text2 = null!;

        private DiffResult _diffs;

        [GlobalSetup]
        public void Setup()
        {
            _text1 = File.ReadAllText("Speedtest1.txt");
            _text2 = File.ReadAllText("Speedtest2.txt");
            _diffs = Diff.Compute(_text1, _text2, 5);
        }

        [Benchmark]
        public object MakeHumanReadable() => _diffs.MakeHumanReadable();

        [Benchmark]
        public object OptimizeForMachineProcessing() => _diffs.OptimizeForMachineProcessing();

        [Benchmark]
        public object PatchCompute() => Patch.Compute(_text1, _text2, 5);

        [Benchmark]
        public void ComputeDiff()
        {
            var diff = Diff.Compute(_text1, _text2, 5);
            diff.CleanupSemantic();
            diff.CleanupEfficiency();
        }

        [Benchmark]
        public object ComputeOnly() => Diff.Compute(_text1, _text2, 5);

        [Benchmark]
        public object ComputeCharMode() => Diff.Compute(_text1, _text2, 5, checklines: false);

        [Benchmark]
        public void ComputeDiffWithLines()
        {
            var diff = Diff.Compute(_text1, _text2, checklines: true);
            diff.CleanupSemantic();
        }

        [Benchmark]
        public void ToDelta()
        {
            var diff = Diff.Compute(_text1, _text2, 5);
            _ = diff.ToDelta();
        }

        [Benchmark]
        public void Text1()
        {
            var diff = Diff.Compute(_text1, _text2, 5);
            _ = diff.Text1();
        }

        [Benchmark]
        public void Text2()
        {
            var diff = Diff.Compute(_text1, _text2, 5);
            _ = diff.Text2();
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
