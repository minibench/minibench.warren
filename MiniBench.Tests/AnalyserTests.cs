using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MiniBench.Core;
using MiniBench.Core.CodeAnalysis;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace MiniBench.Tests
{
    public class AnalyserTests
    {
        private readonly CSharpParseOptions parseOptions = 
            new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.CSharp4);
        private readonly Encoding defaultEncoding = Encoding.UTF8;

        [Fact]
        public void CanAnalyseEmptyClass()
        {
            var code =
@"using System;

namespace MiniBench.Tests
{
    class EmptyTest
    {
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "");

            Assert.Empty(results);
        }

        [Fact]
        public void CanAnalyseSimpleBenchmark()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class SimpleTest
    {
        [Benchmark]
        public void SimpleBenchmark() {}
    }
}";

            Console.WriteLine(code);
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
            Assert.Null(benchmarkInfo.SetupMethod);
        }

        [Fact]
        public void CanAnalyseMultipleBenchmarksInOneClass()
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
        internal void SimpleBenchmark2() {}

        [Benchmark]
        public void SimpleBenchmark3() {}

        public void SimpleNOTBenchmark() {}
    }
}";

            Console.WriteLine(code);
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
        public void CanAnalyseMultipleBenchmarksInMultipleClasses()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class MultiTest1
    {
        [Benchmark]
        public void SimpleBenchmark1() {}

        [Benchmark]
        internal void SimpleBenchmark2() {}

        [Benchmark]
        public void SimpleBenchmark3() {}

        public void SimpleNOTBenchmark() {}
    }

    class MultiTest2
    {
        [Benchmark]
        public void SimpleBenchmark1() {}
    }

    class MultiTest3
    {
        public void SimpleNOTBenchmark() {}
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(4, results.Count());
            Assert.True(results.All(r => r.NamespaceName == "MiniBench.Tests"));

            // class MultiTest1
            Assert.Equal(3, results.Count(r => r.ClassName == "MultiTest1"));
            Assert.Equal(1, results.Count(r => r.ClassName == "MultiTest1" && r.MethodName == "SimpleBenchmark1"));
            Assert.Equal(1, results.Count(r => r.ClassName == "MultiTest1" && r.MethodName == "SimpleBenchmark2"));
            Assert.Equal(1, results.Count(r => r.ClassName == "MultiTest1" && r.MethodName == "SimpleBenchmark3"));
            Assert.Equal(0, results.Count(r => r.ClassName == "MultiTest1" && r.MethodName == "SimpleNOTBenchmark"));

            // class MultiTest2
            Assert.Equal(1, results.Count(r => r.ClassName == "MultiTest2"));
            Assert.Equal(1, results.Count(r => r.ClassName == "MultiTest2" && r.MethodName == "SimpleBenchmark1"));

            // class MultiTest3
            Assert.Equal(0, results.Count(r => r.ClassName == "MultiTest3"));
            Assert.Equal(0, results.Count(r => r.ClassName == "MultiTest3" && r.MethodName == "SimpleNOTBenchmark"));
        }

        [Fact]
        public void CanAnalyseWhenBlackholeIsNeededPredefinedType()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [Benchmark]
        public double SimpleBenchmark()
        {
            return 42.0;
        }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(true, benchmarkInfo.GenerateBlackhole);
        }

        [Fact]
        public void CanAnalyseWhenBlackholeIsNeededIdentifier()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [Benchmark]
        public DateTime SimpleBenchmark()
        {
            return new DateTime(42);
        }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(true, benchmarkInfo.GenerateBlackhole);
        }

        [Fact]
        public void CanAnalyseInjectedParams()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [Benchmark]
        public double SimpleBenchmark(IterationParams iteration)
        {
            return 42.0;
        }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(true, benchmarkInfo.GenerateBlackhole);
            Assert.Equal(new[] { "IterationParams" }, benchmarkInfo.ParametersToInject);
        }

        [Fact]
        public void CanAnalyseParamsForField()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [Params(1, 2, 3, 4, 5, 10)]
        public int SomeField;

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(new ParamsAttribute(1, 2, 3, 4, 5, 10), benchmarkInfo.Params);
            Assert.Null(benchmarkInfo.ParamsWithSteps);
            Assert.Equal("SomeField", benchmarkInfo.ParamsFieldName);
        }

        [Fact]
        public void CanAnalyseParamsWithStepsForField()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [ParamsWithSteps(start:1, end:10, step:1)]
        public int SomeField;

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(new ParamsWithStepsAttribute(1, 10, 1), benchmarkInfo.ParamsWithSteps);
            Assert.Null(benchmarkInfo.Params);
            Assert.Equal("SomeField", benchmarkInfo.ParamsFieldName);
        }

        [Fact]
        public void CanAnalyseParamsForProperty()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [Params(1, 2, 3, 4, 5, 10)]
        public int SomeProperty { get; set; };

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(new ParamsAttribute(1, 2, 3, 4, 5, 10), benchmarkInfo.Params);
            Assert.Null(benchmarkInfo.ParamsWithSteps);
            Assert.Equal("SomeProperty", benchmarkInfo.ParamsFieldName);
        }

        [Fact]
        public void CanAnalyseParamsWithStepsForProperty()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [ParamsWithSteps(start:1, end:10, step:1)]
        public int SomeProperty { get; set; };

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal(new ParamsWithStepsAttribute(1, 10, 1), benchmarkInfo.ParamsWithSteps);
            Assert.Null(benchmarkInfo.Params);
            Assert.Equal("SomeProperty", benchmarkInfo.ParamsFieldName);
        }

        [Fact]
        public void CanAnalyseSetupAttribute()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class SimpleTest
    {
        [Setup]
        public void SetupMethod() {}

        [Benchmark]
        public void SimpleBenchmark() {}
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var results = new Analyser().AnalyseBenchmark(syntaxTree, "TestFile").ToList();

            Assert.Equal(1, results.Count());
            var benchmarkInfo = results.FirstOrDefault();
            Assert.NotNull(benchmarkInfo);

            Assert.Equal("SetupMethod", benchmarkInfo.SetupMethod);
        }
    }
}
