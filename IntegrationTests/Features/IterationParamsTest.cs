using System;
using MiniBench.Core;
using Xunit;

namespace Features
{
    public class IterationParamsTest
    {
        // This has to be static for the test to work!! 
        // The Benchmark Runner new's up a new instance of this class!!
        private static int _demoTestRunCount;
        private static int _iterationsCounter;

        [Fact]
        public void BasicTest()
        {
            _demoTestRunCount = 0;
            _iterationsCounter = 0;
            Options opt = new OptionsBuilder()
                    .Include(typeof(IterationParamsTest))
                    .WarmupRuns(0)
                    .Runs(1)
                    .Build();
            new Runner(opt).Run();

            Console.WriteLine("_demoTestRunCount = {0}, _iterations.Count = {1}", _demoTestRunCount, _iterationsCounter);
            Assert.True(_demoTestRunCount > 0, "Expected the Benchmark method to be run at least once: " + _demoTestRunCount);
            Assert.True(_iterationsCounter > 0, "Expected the Benchmark iterations counter to be > 0: " + _iterationsCounter);
        }

        [Benchmark]
        public double IterationParamsBenchmark(IterationParams iteration)
        {
            // It seems like Assert Failiures inside the Benchmark, don't actually fail the test, although
            // there is a message printed in the test output. Probably because we create a new instance of
            // the class inside the Benchmark, we don't use the same one as the test runner?
            // Or maybe because the Benchmark runner swallows the exceptions, so the Unit-test runner can't see them?!
            Assert.NotNull(iteration);

            if (iteration.Count == 0)
                _iterationsCounter = 0;

            if (_iterationsCounter != 0)
            {
                Assert.True(iteration.Count > _iterationsCounter,
                            "Expected current: " + iteration.Count + " to be > than previous: " + _iterationsCounter);
            }
            if (iteration.TotalCount != 0)
            {
                Assert.True(iteration.Count <= iteration.TotalCount,
                            "Expected Count: " + iteration.Count + " to be <= TotalCount: " + iteration.TotalCount);
            }

            _iterationsCounter = iteration.Count;
            _demoTestRunCount++;

            return Math.Sqrt(123.456);
        }
    }
}
