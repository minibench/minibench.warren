using System;
using System.Collections.Generic;
using System.Linq;
using MiniBench.Core;
using Xunit;

namespace Features
{
    public class ParamsWithStepsTest
    {
        // This has to be static for the test to work!! 
        // The Benchmark Runner new's up a new instance of this class!!
        private static HashSet<int> _params;
        private static int _demoTestRunCount;

        [Fact]
        public void BasicTest()
        {
            _params = new HashSet<int>();
            _demoTestRunCount = 0;
            Options opt = new OptionsBuilder()
                    .Include(typeof(ParamsWithStepsTest))
                    .WarmupRuns(0)
                    .Runs(1)
                    .Build();
            new Runner(opt).Run();

            Console.WriteLine("demoTestRunCount = {0}, params.Count = {1}, params = {2}", _demoTestRunCount, _params.Count, String.Join(", ", _params));
            Assert.True(_demoTestRunCount > 0, "Expected the Benchmark method to be run at least once: " + _demoTestRunCount);
            // We get 0 (default(int)) passed in during warm-up, so include that as well!!
            Assert.Equal(Enumerable.Range(0, 10), _params);
        }

        [ParamsWithSteps(start:1, end:10, step:1)]
#pragma warning disable 649
        // This must be public, writable and field/property?!?
        public int Param { get; set; }
#pragma warning restore 649

        [Benchmark]
        public double IterationParamsBenchmark(IterationParams iteration) //, [ParamsWithSteps(1, 10, 1)]int param)
        {
            if (_params.Contains(Param) == false)
                _params.Add(Param);
            _demoTestRunCount++;

            return Math.Sqrt(123.456);
        }
    }
}
