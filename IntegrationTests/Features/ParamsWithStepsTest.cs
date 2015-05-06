using MiniBench.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Features
{
    public class ParamsWithStepsTestProperty
    {
        // This has to be static for the test to work!! 
        // The Benchmark Runner new's up a new instance of this class!!
        private static HashSet<int> _params;
        private static long _demoTestRunCount;

        [Fact]
        public void BasicTest()
        {
            _params = new HashSet<int>();
            _demoTestRunCount = 0;
            Options opt = new OptionsBuilder()
                    .Include(this.GetType())
                    .WarmupRuns(0)
                    .Runs(1)
                    .InvocationsPerRun(1)
                    .Build();
            new Runner(opt).Run();

            Console.WriteLine("demoTestRunCount = {0}, params.Count = {1}, params = {2}", _demoTestRunCount, _params.Count, String.Join(", ", _params));
            Assert.True(_demoTestRunCount > 0, "Expected the Benchmark method to be run at least once: " + _demoTestRunCount);
            Assert.Equal(Enumerable.Range(1, 10), _params);
        }

#pragma warning disable 649 // we know that the Benchmark with write/read this field
        [ParamsWithSteps(start:1, end:10, step:1)]
        public int Param { get; set; }
#pragma warning restore 649

        [Benchmark]
        public double Benchmark()
        {
            if (_params.Contains(Param) == false)
                _params.Add(Param);
            _demoTestRunCount++;

            return Math.Sqrt(123.456);
        }
    }

    public class ParamsWithStepsTestField
    {
        // This has to be static for the test to work!! 
        // The Benchmark Runner new's up a new instance of this class!!
        private static HashSet<int> _params;
        private static long _demoTestRunCount;

        [Fact]
        public void BasicTest()
        {
            _params = new HashSet<int>();
            _demoTestRunCount = 0;
            Options opt = new OptionsBuilder()
                    .Include(this.GetType())
                    .WarmupRuns(0)
                    .Runs(1)
                    .InvocationsPerRun(1)
                    .Build();
            new Runner(opt).Run();

            Console.WriteLine("demoTestRunCount = {0}, params.Count = {1}, params = {2}", _demoTestRunCount, _params.Count, String.Join(", ", _params));
            Assert.True(_demoTestRunCount > 0, "Expected the Benchmark method to be run at least once: " + _demoTestRunCount);
            Assert.Equal(Enumerable.Range(1, 10), _params);
        }

        // This must be public, writable and field/property?!?
        [ParamsWithSteps(start: 1, end: 10, step: 1)]
        public int Param = 0;

        [Benchmark]
        public double Benchmark()
        {
            if (_params.Contains(Param) == false)
                _params.Add(Param);
            _demoTestRunCount++;

            return Math.Sqrt(123.456);
        }
    }
}
