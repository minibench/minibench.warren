﻿using System;
using System.Collections.Generic;
using MiniBench.Core.Infrastructure;

namespace MiniBench.Core.Profiling
{
    internal class Profiler
    {
        internal readonly Dictionary<IInternalProfiler, AggregatedProfilerResult []> Profilers =
            new Dictionary<IInternalProfiler, AggregatedProfilerResult []>
            {
                //{ new GCProfiler(), null }
            };

        private CommandLineArgs arguments;

        internal Profiler(CommandLineArgs arguments)
        {
            this.arguments = arguments;

            //var profilerToRun = 
        }

        internal void BeforeIteration()
        {
            foreach (var profiler in Profilers)
            {
                profiler.Key.BeforeIteration();
            }
        }

        internal void AfterIteration()
        {
            try
            {
                var keysCopy = new IInternalProfiler[Profilers.Keys.Count];
                Profilers.Keys.CopyTo(keysCopy, 0);
                foreach (IInternalProfiler profiler in keysCopy)
                {
                    IList<ProfilerResult> results = profiler.AfterIteration();
                    if (Profilers[profiler] == null && results.Count > 0)
                    {
                        var aggregatedResult = new AggregatedProfilerResult[results.Count];
                        for (int i = 0; i < results.Count; i++)
                        {
                            aggregatedResult[i] = new AggregatedProfilerResult
                                (
                                    results[i].Name,
                                    results[i].Units,
                                    results[i].AggregationMode
                                );
                            aggregatedResult[i].RawResults.Add(results[i].Value);
                        }
                        Profilers[profiler] = aggregatedResult;
                    }
                    else
                    {
                        for (int i = 0; i < results.Count; i++)
                        {
                            Profilers[profiler][i].RawResults.Add(results[i].Value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO where does the Exception bubble up to if we don't have a try-catch here??
                Console.WriteLine("Profiler: " + ex.ToString());
                Console.WriteLine(ex.StackTrace);
            }
        }

        internal void PrintIterationResults()
        {
            try
            {
                foreach (var profiler in Profilers)
                {
                    if (profiler.Value != null)
                    {
                        Array.ForEach(profiler.Value, result =>
                            {
                                Console.WriteLine("Result {0,36}: {1:N0} {2} ({3})", result.Name,
                                                  result.RawResults[result.RawResults.Count - 1], result.Units, result.Mode);
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO where does the Exception bubble up to if we don't have a try-catch here??
                Console.WriteLine("Profiler: " + ex.ToString());
                Console.WriteLine(ex.StackTrace);
            }
        }

        internal void PrintOverallResults()
        {
            try
            {
                foreach (var profiler in Profilers)
                {
                    if (profiler.Value != null)
                    {
                        Array.ForEach(profiler.Value, result =>
                            {
                                Console.WriteLine("Aggregated Result {0,25}: {1:N0} {2} ({3})",
                                                  result.Name, result.AggregatedValue, result.Units, result.Mode);
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO where does the Exception bubble up to if we don't have a try-catch here??
                Console.WriteLine("Profiler: " + ex.ToString());
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
