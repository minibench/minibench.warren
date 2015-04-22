using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Text;
using Xunit;

namespace MiniBench.Tests
{
    public class AnalyserErrorTests
    {
        private readonly CSharpParseOptions parseOptions =
            new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.CSharp4);
        private readonly Encoding defaultEncoding = Encoding.UTF8;

        [Fact]
        public void BenchmarkMethodsMustBePublicOrInternal()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    class SimpleTest // no modifier implies internal
    {
        [Benchmark]
        private void SimpleBenchmark() {}
    }
}";
            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Methods annotated with [Benchmark] must be public", exception.Message);
        }

        [Fact]
        public void ClassesContainingBenchmarkMethodsMustBePublicOrInternal()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    private class SimpleTest
    {
        [Benchmark]
        public void SimpleBenchmark() {}
    }
}";
            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Classes containing methods annotated with [Benchmark] must be public", exception.Message);
        }

        [Fact]
        public void BenchmarkMethodsOnlyAcceptAllowedParameters()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class SimpleTest
    {
        [Benchmark]
        public void SimpleBenchmark(int notAllowed, IterationParams allowed) {}
    }
}";
            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Methods annotated with [Benchmark] can only accept allowed parameters", exception.Message);
        }

        [Fact]
        public void OnlyOneFieldOrPropertyCanBeMarkedWithParamsWithSteps()
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

        [Params(1, 2, 3)]
        public int SomeOtherProperty { get; set; };

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Only one field/property can be annotated with [Params] or [ParamsWithSteps]", exception.Message);
        }

        [Fact]
        public void ParamsAttributeMustHaveAtLeastOneValue()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [Params()]
        public int SomeProperty { get; set; };

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("The [Params] attribute must be used with at least 1 value", exception.Message);
        }

        [Fact]
        public void ParamsWithStepsAttributeCannotBeAppliedToMultipleFields()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [ParamsWithSteps(start:1, end:10, step:1)]
        public int SomeField, SomeOtherField;

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Only one a single field can be annotated with [Params] or [ParamsWithSteps]", exception.Message);
        }

        [Fact]
        public void ParamsAttributeMustBeAppliedToPublicFields()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [Params(1, 2, 3)]
        private int SomeField;

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Fields annotated with [Params] or [ParamsWithSteps] must be public", exception.Message);
        }

        [Fact]
        public void ParamsAttributeMustBeAppliedToPublicWriteableProperties()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class BlackholeTest
    {
        [Params(1, 2, 3)]
        public int SomeField { set; }; // No get, so not writable

        [Benchmark]
        public void SimpleBenchmark() { }
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Properties annotated with [Params] or [ParamsWithSteps] must be public and writable", exception.Message);
        }

        [Fact]
        public void SetupAttributeMustBeAppliedToMethodsWithNoParameters()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class SimpleTest
    {
        [Setup]
        public void SetupMethod(int notAllowed) {}

        [Benchmark]
        public void SimpleBenchmark() {}
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Methods annotated with [Setup] must have no parameters", exception.Message);
        }

        [Fact]
        public void SetupAttributeMustBeAppliedToMethodsThatArePublicOrInternal()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class SimpleTest
    {
        [Setup]
        private void SetupMethod() {}

        [Benchmark]
        public void SimpleBenchmark() {}
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Methods annotated with [Setup] must be public or internal", exception.Message);
        }

        [Fact]
        public void SetupAttributeCanOnlyBeAppliedToOneMethodInAClass()
        {
            var code =
@"using System;
using MiniBench.Core;

namespace MiniBench.Tests
{
    public class SimpleTest
    {
        [Setup]
        public void SetupMethodOne() {}

        [Setup]
        public void SetupMethodTwo() {}

        [Benchmark]
        public void SimpleBenchmark() {}
    }
}";

            Console.WriteLine(code);
            var syntaxTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, encoding: defaultEncoding);
            var exception = Assert.Throws<InvalidOperationException>(() => new Analyser().AnalyseBenchmark(syntaxTree, "TestFile"));
            Console.WriteLine("Error: " + exception.Message);
            Assert.Contains("Only one method can be annotated with [Setup]", exception.Message);
        }
    }
}
