using System.Collections.ObjectModel;

namespace MiniBench.Core
{
    internal interface IBenchmarkTarget
    {
        string Namespace { get; }

        string Type { get; }

        string Method { get; }

        ReadOnlyCollection<string> Categories { get; }

        BenchmarkResult RunTest(Options options);
    }
}
