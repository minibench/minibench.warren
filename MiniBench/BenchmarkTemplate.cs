using System.Text;

namespace MiniBench
{
    class BenchmarkTemplate
    {
        private static string namespaceReplaceText = "##NAMESPACE-NAME##";
        private static string classReplaceText = "##CLASS-NAME##";
        private static string methodReplaceText = "##METHOD-NAME##";
        private static string warmupMethodCallReplaceText = "##WARMUP-METHOD-CALL##";
        private static string benchmarkMethodCallReplaceText = "##BENCHMARK-METHOD-CALL##";
        private static string generatedClassReplaceText = "##GENERAGED-CLASS-NAME##";

        private static string paramsStartCodeReplaceText = "##PARAMS-START-CODE##";
        private static string paramsEndCodeReplaceText = "##PARAMS-END-CODE##";

        private static string benchmarkHarnessTemplate =
@"using MiniBench.Core;
using MiniBench.Core.Profiling;
using ##NAMESPACE-NAME##;
using System;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace MiniBench.Benchmarks
{
    public class ##GENERAGED-CLASS-NAME## : MarshalByRefObject, IBenchmarkTarget
    {
        private readonly string @namespace = ""##NAMESPACE-NAME##"";
        public string Namespace { get { return @namespace; } }

        private readonly string @type = ""##CLASS-NAME##"";
        public string Type { get { return @type; } }

        private readonly string @method = ""##METHOD-NAME##"";
        public string Method { get { return @method; } }

        private readonly ReadOnlyCollection<string> categories;
        public ReadOnlyCollection<string> Categories { get { return categories; } }

        private readonly Blackhole blackhole = new Blackhole();

        private readonly Profiler profiler;

        internal ##GENERAGED-CLASS-NAME##(Profiler profiler)
        {
            // TODO Eventually we need to get this from the Benchmark itself, for the time being just use a placeholder
            categories = new ReadOnlyCollection<string>(new String [] { ""Testing"" } );
            this.profiler = profiler;
        }

        public BenchmarkResult RunTest(Options options)
        {
            try
            {
                Console.WriteLine(""Running benchmark: {0}.{1}"", @type, @method);
                ##CLASS-NAME## benchmarkClass = GetBenchmarkClass();

                IterationParams warmupIterations = new IterationParams();
                warmupIterations.Count = 0;

                IterationParams iterations = new IterationParams();

                //System.Diagnostics.Debugger.Launch();
                //System.Diagnostics.Debugger.Break();

                // Make sure the method is JIT-compiled.
                ##WARMUP-METHOD-CALL##;

                GC.Collect();
                GC.WaitForPendingFinalizers();

                //System.Diagnostics.Debugger.Launch();

                Stopwatch stopwatch = new Stopwatch();

                ##PARAMS-START-CODE##

                long ticks = (long)(Stopwatch.Frequency * options.WarmupTime.TotalSeconds);
                stopwatch.Reset();
                stopwatch.Start();
                while (stopwatch.ElapsedTicks < ticks)
                {
                    ##WARMUP-METHOD-CALL##;
                    warmupIterations.Count++;
                }
                stopwatch.Stop();
                Console.WriteLine(""Warmup:    {0,12:N0} iterations in {1,10:N3} ms, {2,6:N3} ns/op"", 
                                    warmupIterations.Count, stopwatch.Elapsed.TotalMilliseconds, Utils.TicksToNanoseconds(stopwatch) / warmupIterations.Count);

                double ratio = options.TargetTime.TotalSeconds / stopwatch.Elapsed.TotalSeconds;
                iterations.TotalCount = (int)(warmupIterations.Count * ratio);
                GC.Collect();
                GC.WaitForPendingFinalizers();

                for (int batch = 0; batch < options.Runs; batch++)
                {
                    iterations.Batch = batch;
                    profiler.BeforeIteration();
                    stopwatch.Reset();
                    stopwatch.Start();
                    for (iterations.Count = 0; iterations.Count < iterations.TotalCount; iterations.Count++)
                    {
                        ##BENCHMARK-METHOD-CALL##;
                    }
                    stopwatch.Stop();
                    profiler.AfterIteration();

                    Console.WriteLine(""Benchmark: {0,12:N0} iterations in {1,10:N3} ms, {2,6:N3} ns/op"", 
                                        iterations.Count, stopwatch.Elapsed.TotalMilliseconds, Utils.TicksToNanoseconds(stopwatch) / iterations.Count);

                    profiler.PrintIterationResults();
                }

                ##PARAMS-END-CODE##

                // Need to collect the results from the multiple ""params"" runs and return a list, now a single result
                return BenchmarkResult.ForSuccess(this, iterations.TotalCount, stopwatch.Elapsed);
            }
            catch (Exception e)
            {
                // TODO: Stack trace?
                Console.WriteLine(""ERROR in Benchmark: "" + e.Message);
                Console.WriteLine(e.ToString());
                return BenchmarkResult.ForFailure(this, e.ToString());
            }
        }

        private ##CLASS-NAME## benchmarkClass = null;
        private ##CLASS-NAME## GetBenchmarkClass()
        {
            if (benchmarkClass == null)
                benchmarkClass = new ##CLASS-NAME##();
            return benchmarkClass;
        }
    }
}";


        internal static string ProcessCodeTemplates(BenchmarkInfo info)
        {
            // TODO at some point, we might need a less-hacky templating mechanism?!
            // Maybe Razor? see https://github.com/volkovku/RazorTemplates

            var benchmarkMethodCall = GetMethodCallWithParameters(info);
            var warmupMethodCall = GetMethodCallWithParameters(info, warmupMethod: true);

            string paramsStartCode = "", paramsEndCode = "";

            if (info.ParamsWithSteps != null && info.ParamsFieldName != null)
            {
                paramsStartCode =
"for (int param = " + info.ParamsWithSteps.Start + "; param <= " + info.ParamsWithSteps.End + "; param += " + info.ParamsWithSteps.Step + ")" +
"\n{\n" + "benchmarkClass." + info.ParamsFieldName + " = param;\n";

                paramsEndCode = "}\n";
            }

            var generatedBenchmark = benchmarkHarnessTemplate
                                .Replace(namespaceReplaceText, info.NamespaceName)
                                .Replace(classReplaceText, info.ClassName)
                                .Replace(methodReplaceText, info.MethodName)
                                .Replace(warmupMethodCallReplaceText, warmupMethodCall)
                                .Replace(benchmarkMethodCallReplaceText, benchmarkMethodCall)
                                .Replace(generatedClassReplaceText, info.GeneratedClassName)
                                .Replace(paramsStartCodeReplaceText, paramsStartCode)
                                .Replace(paramsEndCodeReplaceText, paramsEndCode);

            return generatedBenchmark;
        }

        private static string GetMethodCallWithParameters(BenchmarkInfo info, bool warmupMethod = false)
        {
            var methodParameters = new StringBuilder();
            foreach (var paramater in info.ParametersToInject)
            {
                switch (paramater)
                {
                    case "IterationParams":
                        methodParameters.Append(warmupMethod ? "warmupIterations" : "iterations");
                        break;
                    // TODO Add in "BenchmarkParams" support
                    //case "BenchmarkParams":
                }
            }

            string methodCallWithParameters = string.Format("benchmarkClass.{0}({1})", info.MethodName, methodParameters);
            if (info.GenerateBlackhole)
                methodCallWithParameters = string.Format("blackhole.Consume({0})", methodCallWithParameters);

            return methodCallWithParameters;
        }
    }
}
