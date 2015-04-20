namespace MiniBench.Core
{
    /// <summary>
    /// Helper class that can be "injected" into a Benchmark method, if uses as a paramter type, e.g.
    /// <example>
    /// <code>
    /// [Benchmark]
    /// public int MyBenchmark(IterationParams iteration)
    /// {
    ///     ..
    ///     var loopCounter = iteration.Count;
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public class IterationParams
    {
        public long Count { get; internal set; }
        public long TotalCount { get; internal set; }
        public long Batch { get; internal set; }
    }
}
