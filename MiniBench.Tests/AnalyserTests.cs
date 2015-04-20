using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MiniBench.Core;
using Xunit;

namespace MiniBench.Tests
{
    public class AnalyserTests
    {
        private readonly CSharpParseOptions parseOptions = 
            new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.CSharp4);
        private readonly Encoding defaultEncoding = Encoding.UTF8;

        [Fact]
        public void AnalyseBenchmark_CanAnalyseEmptyClass()
        {
            var code =
@"using System;

namespace MiniBench.Tests
{
    class EmptyTest
    {
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "");

            Assert.Empty(results);
        }

        [Fact]
        public void AnalyseBenchmark_CanAnalyseSimpleBenchmark()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class SimpleTest
    {
        [Benchmark]
        public void SimpleBenchmark() {}
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal("MiniBench.Tests", benchmarkInfo.NamespaceName);
            Assert.Equal("SimpleTest", benchmarkInfo.ClassName);
            Assert.Equal("SimpleBenchmark", benchmarkInfo.MethodName);
            Assert.Equal("TestFile_MiniBench_Tests_SimpleTest_SimpleBenchmark", benchmarkInfo.GeneratedClassName);

            Assert.Equal(false, benchmarkInfo.GenerateBlackhole);
            Assert.Empty(benchmarkInfo.ParametersToInject);
            Assert.Null(benchmarkInfo.ParamsWithSteps);
            Assert.Null(benchmarkInfo.ParamsFieldName);
        }

        [Fact]
        public void AnalyseBenchmark_CanAnalyseMultipleBenchmarksInOneClass()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class MultiTest
    {
        [Benchmark]
        public void SimpleBenchmark1() {}

        [Benchmark]
        public void SimpleBenchmark2() {}

        [Benchmark]
        public void SimpleBenchmark3() {}

        public void SimpleNOTBenchmark() {}
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(3, results.Count());

            Assert.True(results.All(r => r.NamespaceName == "MiniBench.Tests"));
            Assert.True(results.All(r => r.ClassName == "MultiTest"));

            Assert.Equal(1, results.Count(r => r.MethodName == "SimpleBenchmark1"));
            Assert.Equal(1, results.Count(r => r.MethodName == "SimpleBenchmark2"));
            Assert.Equal(1, results.Count(r => r.MethodName == "SimpleBenchmark3"));
            Assert.Equal(0, results.Count(r => r.MethodName == "SimpleNOTBenchmark"));
        }

        [Fact]
        public void AnalyseBenchmark_CanAnalyseWhenBlackholeIsNeededPredefinedType()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class BlackholeTest
    {
        [Benchmark]
        public double SimpleBenchmark()
        {
            return 42.0;
        }
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(true, benchmarkInfo.GenerateBlackhole);
        }

        [Fact]
        public void AnalyseBenchmark_CanAnalyseWhenBlackholeIsNeededIdentifier()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class BlackholeTest
    {
        [Benchmark]
        public DateTime SimpleBenchmark()
        {
            return new DateTime(42);
        }
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(true, benchmarkInfo.GenerateBlackhole);
        }

        [Fact]
        public void AnalyseBenchmark_CanAnalyseInjectedParams()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class BlackholeTest
    {
        [Benchmark]
        public double SimpleBenchmark(IterationParams iteration)
        {
            return 42.0;
        }
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(true, benchmarkInfo.GenerateBlackhole);
            Assert.Equal(new[] { "IterationParams" }, benchmarkInfo.ParametersToInject);
        }

        [Fact]
        public void AnalyseBenchmark_CanAnalyseParamsWithStepsForField()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class BlackholeTest
    {
        [ParamsWithSteps(start:1, end:10, step:1)]
        public int SomeField;

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(new ParamsWithStepsAttribute(1, 10, 1), benchmarkInfo.ParamsWithSteps);
            Assert.Equal("SomeField", benchmarkInfo.ParamsFieldName);
        }

        [Fact]
        public void AnalyseBenchmark_CanAnalyseParamsWithStepsForProperty()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class BlackholeTest
    {
        [ParamsWithSteps(start:1, end:10, step:1)]
        public int SomeProperty { get; set; };

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(new ParamsWithStepsAttribute(1, 10, 1), benchmarkInfo.ParamsWithSteps);
            Assert.Equal("SomeProperty", benchmarkInfo.ParamsFieldName);
        }
    }
}
