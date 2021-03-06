﻿using System.Diagnostics;

namespace MiniBench.Core.Infrastructure
{
    internal static class Utils
    {
        public static double TicksToNanoseconds(Stopwatch timer)
        {
            // 1 millisecond (ms) = 1/1000th of a second or 1000 µs
            // 1 microsecond (µs) = 1/1000th of a ms or 1000 ns
            // 1 nanoseconds (ns) = 1/1000th of a µs

            //seconds = ticks / frequency
            //ms = (ticks / frequency) * 1000
            //nanoseconds = (ticks / frequency) * 1000000000

            double nanosPerSecond = 1000000000; //1,000,000,000
            return (timer.ElapsedTicks / (double)Stopwatch.Frequency) * nanosPerSecond;
        }
    }
}
