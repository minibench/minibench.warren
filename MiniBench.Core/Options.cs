using System;

namespace MiniBench.Core
{
    public sealed class Options
    {
        private readonly Type benchmarkType;
        public Type BenchmarkType { get { return benchmarkType; } }

        private readonly string benchmarkPrefix;
        public string BenchmarkPrefix { get { return benchmarkPrefix; } }

        private readonly string benchmarkRegex;
        public string BenchmarkRegex { get { return benchmarkRegex; } }

        public uint WarmupRuns { get; private set; }
        public uint Runs { get; private set; }
        public uint? InvocationsPerRun { get; private set; }

        private TimeSpan warmupTime = TimeSpan.FromSeconds(10);
        public TimeSpan WarmupTime { get { return warmupTime; } }

        private TimeSpan targetTime = TimeSpan.FromSeconds(10);
        public TimeSpan TargetTime { get { return warmupTime; } }

        private static readonly string GeneratedPrefix = "Generated_Runner";

        internal Options(Type benchmarkType, uint warmupRuns, uint runs, uint? invocationsPerRun = null)
            : this(warmupRuns, runs, invocationsPerRun)
        {
            this.benchmarkType = benchmarkType;
            this.benchmarkPrefix = string.Format("{0}_{1}_{2}",
                                            GeneratedPrefix,
                                            benchmarkType.Namespace.Replace('.', '_'),
                                            benchmarkType.Name);
        }

        internal Options(String benchmarkRegex, uint warmupRuns, uint runs, uint? invocationsPerRun = null)
            :this(warmupRuns, runs, invocationsPerRun)
        {
            this.benchmarkRegex = benchmarkRegex;
        }

        private Options(uint warmupRuns, uint runs, uint? invocationsPerRun = null)
        {
            WarmupRuns = warmupRuns;
            Runs = runs;
            InvocationsPerRun = invocationsPerRun;
        }
    }
}