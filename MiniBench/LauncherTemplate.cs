namespace MiniBench
{
    internal class LauncherTemplate
    {
        private static string benchmarkLauncherTemplate =
@"using System;
using System.Reflection;
using System.Collections.Generic;
using MiniBench.Core;
using MiniBench.Core.Infrastructure;
using MiniBench.Core.Profiling;

namespace MiniBench.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            CommandLineArgs commandLineArgs = CommandLineArgs.SetupCommandLineArgs(args);
            if (commandLineArgs.ShouldExit)
                return;

            Console.WriteLine(""Environment Version: "" + Environment.Version);
            Console.WriteLine(""Executing Assembly - Image Runtime Version: "" + Assembly.GetExecutingAssembly().ImageRuntimeVersion);

            string benchmarkPrefix = ""Generated_Runner_"";
            if (commandLineArgs.ListBenchmarks)
            {
                // Print out all the available benchmarks
                Assembly assembly = Assembly.GetExecutingAssembly();
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsClass && type.IsPublic && !type.IsAbstract 
                        && typeof(IBenchmarkTarget).IsAssignableFrom(type))
                    {
                        Console.WriteLine(type.Name.Replace(benchmarkPrefix, String.Empty)
                                                   .Replace(""_"", "".""));
                    }
                }
                return;
            }
            else if (commandLineArgs.ListProfilers)
            {
                Profiler profiler = new Profiler(commandLineArgs);
                if (profiler.AvailableProfilers.Count == 0)
                {
                     Console.WriteLine(""There are no profilers registered!"");
                }
                else
                {
                    Console.WriteLine(""Available Profilers:"");
                    foreach (var internalProfiler in profiler.AvailableProfilers)
                    {
                        Console.WriteLine(""{0} - {1}"", internalProfiler.Name, internalProfiler.SummaryText);
                    }
                    Console.WriteLine();
                }
                return;
            }
            else
            {
                Console.WriteLine(""Profiler: "" + commandLineArgs.ProfilerToRun);
                Options opt = new OptionsBuilder()
                                    .Include(commandLineArgs.BenchmarksToRun)
                                    .Build();
                new Runner(opt).Run();
            }
        }
    }
}";

        internal static string ProcessLauncherTemplate()
        {
            return benchmarkLauncherTemplate;
        }
    }
}
