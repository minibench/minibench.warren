using System;
using System.Collections.Generic;

namespace MiniBench.Core.Profiling
{
    internal class GCProfiler : IInternalProfiler
    {
        private int beforeGen0, beforeGen1, beforeGen2;
        private long memoryBefore;

        public string Name
        {
            get { return "GCProfiler"; }
        }

        public string SummaryText
        {
            get
            {
                return
                    "Calculates the GC Collection Counts for Generations 0, 1 and 2. " +
                    "Also calculates the memory usage (per iteration) and the peak during the entire run";
            }
        }

        public void BeforeIteration()
        {
            beforeGen0 = GC.CollectionCount(0);
            beforeGen1 = GC.CollectionCount(1);
            beforeGen2 = GC.CollectionCount(2);
            memoryBefore = GC.GetTotalMemory(false);
            Console.WriteLine("BeforeIteration: Gen0={0:N0}, Gen1={1:N0}, Gen2={2:N0}, Memory={3:N0}", beforeGen0, beforeGen1, beforeGen2, memoryBefore);
        }

        public IList<ProfilerResult> AfterIteration()
        {
            var afterGen0 = GC.CollectionCount(0);
            var afterGen1 = GC.CollectionCount(1);
            var afterGen2 = GC.CollectionCount(2);
            var memoryAfter = GC.GetTotalMemory(false);
            Console.WriteLine("AfterIteration:  Gen0={0:N0} ({1:N0}), Gen1={2:N0} ({3:N0}), Gen2={4:N0} ({5:N0}), Memory={6:N0} ({7:N0})",
                              afterGen0, afterGen0 - beforeGen0, 
                              afterGen1, afterGen1 - beforeGen1,
                              afterGen2, afterGen2 - beforeGen2, 
                              memoryAfter, memoryAfter - memoryBefore);

            return new []
                {
                    new ProfilerResult("GC.Gen0", afterGen0 - beforeGen0, "counts", AggregationMode.Sum),
                    new ProfilerResult("GC.Gen1", afterGen1 - beforeGen1, "counts", AggregationMode.Sum),
                    new ProfilerResult("GC.Gen2", afterGen2 - beforeGen2, "counts", AggregationMode.Sum),
                    new ProfilerResult("Memory.Usage", memoryAfter - memoryBefore, "bytes", AggregationMode.Max),
                    new ProfilerResult("Memory.Peak", memoryAfter, "bytes", AggregationMode.Max),
                };
        }
    }
}
