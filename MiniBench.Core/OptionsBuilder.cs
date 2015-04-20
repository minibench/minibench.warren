using System;

namespace MiniBench.Core
{
    public sealed class OptionsBuilder
    {
        private Type benchmarkType;
        private String benchmarkRegex;

        private bool useType = false;
        private uint runs = 5;       // Default to 5 runs
        private uint warmupRuns = 5; // Default to 5 warmup runs

        private uint? invocationsPerRun = null;

        public OptionsBuilder Include(Type benchmarkType)
        {
            this.benchmarkType = benchmarkType;
            useType = true;
            return this;
        }

        public OptionsBuilder Include(String benchmarkRegex)
        {
            this.benchmarkRegex = benchmarkRegex;
            useType = false;
            return this;
        }

        public OptionsBuilder WarmupRuns(uint warmupRuns)
        {
            this.warmupRuns = warmupRuns;
            return this;
        }

        public OptionsBuilder Runs(uint runs)
        {
            this.runs = runs;
            return this;
        }

        /// <summary>
        /// This is just here for integration testing, it SHOULDN'T be used in real benchmarks
        /// </summary>
        internal OptionsBuilder InvocationsPerRun(uint invocationsPerRun)
        {
            this.invocationsPerRun = invocationsPerRun;
            return this;
        }

        public Options Build()
        {
            if (useType)
                return new Options(benchmarkType, warmupRuns: warmupRuns, runs: runs, invocationsPerRun: invocationsPerRun);

            return new Options(benchmarkRegex, warmupRuns: warmupRuns, runs: runs, invocationsPerRun: invocationsPerRun);
        }
    }
}
