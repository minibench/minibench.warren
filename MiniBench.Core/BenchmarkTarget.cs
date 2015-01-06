﻿// Copyright 2014 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Remoting;

namespace MiniBench.Core
{
    public sealed class BenchmarkTarget : MarshalByRefObject
    {
        // The non-generic Action delegate was introduced in .NET 3.5.
        // This is the equivalent.
        private delegate void MinibenchAction();

        private readonly Type type;
        private readonly MethodInfo method;

        public string Namespace { get { return type.Namespace; } }

        public string Type { get { return type.Name; } }

        public string Method { get { return method.Name; } }

        private readonly ReadOnlyCollection<string> categories;
        public ReadOnlyCollection<string> Categories { get { return categories; } }  

        internal BenchmarkTarget(Type type, MethodInfo method)
        {
            this.type = type;
            this.method = method;
            // TODO: Get categories from type and method.
            this.categories = new ReadOnlyCollection<string>(new List<string>());
        }

        /// <summary>
        /// Runs a test of the given benchmark target
        /// </summary>
        /// <returns>The result of the test.</returns>
        public BenchmarkResult RunTest(TimeSpan warmupTime, TimeSpan targetTime)
        {
            try
            {
                object instance = Activator.CreateInstance(type);
                MinibenchAction action = (MinibenchAction) Delegate.CreateDelegate(typeof(MinibenchAction), instance, method);
                // Make sure the method is JIT-compiled.
                action();
                long ticks = (long) (Stopwatch.Frequency * warmupTime.TotalSeconds);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Stopwatch stopwatch = Stopwatch.StartNew();
                long warmupIterations = 0;
                while (stopwatch.ElapsedTicks < ticks)
                {
                    action();
                    warmupIterations++;
                }
                stopwatch.Stop();
                Console.WriteLine("{0} iterations in {1}ms", warmupIterations, (long) stopwatch.ElapsedMilliseconds);
                double ratio = targetTime.TotalSeconds / stopwatch.Elapsed.TotalSeconds;
                long iterations = (long) (warmupIterations * ratio);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                stopwatch.Reset();
                stopwatch.Start();
                for (long iteration = 0; iteration < iterations; iteration++)
                {
                    action();
                }
                stopwatch.Stop();
                return BenchmarkResult.ForSuccess(this, iterations, stopwatch.Elapsed);
            }
            catch (Exception e)
            {
                // TODO: Stack trace?
                return BenchmarkResult.ForFailure(this, e.ToString());
            }
        }

        public override string ToString()
        {
            return string.Format("{0}.{1}", type.FullName, method.Name);
        }
    }
}
