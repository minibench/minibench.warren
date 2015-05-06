using MiniBench.Core;
using System;
using System.Collections.Generic;
using Xunit;

namespace Features
{
    public class SetupTest
    {
        // This has to be static for the test to work!! 
        // The Benchmark Runner new's up a new instance of this class!!
        private static long _demoTestRunCount;

        // Delibrately set it to null here
        private static HashSet<int> _collector = null;

        [Fact]
        public void BasicTest()
        {
            _demoTestRunCount = 0;
            // Delibrately don't initialise _collector here, rely on SetupMethod() running
            Options opt = new OptionsBuilder()
                    .Include(typeof(SetupTest))
                    .WarmupRuns(0)
                    .Runs(1)
                    .Build();
            new Runner(opt).Run();

            Console.WriteLine("_demoTestRunCount = {0}, _collector.Count = {1}", _demoTestRunCount, _collector != null ? _collector.Count : -1);
            Assert.True(_demoTestRunCount > 0, "Expected the Benchmark method to be run at least once: " + _demoTestRunCount);
            Assert.NotNull(_collector);
            Assert.True(_collector.Contains(1), "Expected _collector to contain 1");
        }

        [Setup]
        public void SetupMethod()
        {
            Console.WriteLine("SetupMethod() called");
            _collector = new HashSet<int>();
        }

        [Benchmark]
        public double Benchmark()
        {
            _demoTestRunCount++;
            // We can only do this, if SetupMethod() has been run, otherwise _collector will be null
            _collector.Add(1);
            return Math.Sqrt(123.456);
        }
    }
}
