using MiniBench.Core;

namespace MiniBench.Demo.Samples.General
{
    // Inspired by http://blogs.microsoft.co.il/sasha/2013/10/17/on-stackalloc-performance-and-the-large-object-heap/
    // and http://stackoverflow.com/questions/8472655/c-sharp-net-stackalloc (including http://pastie.org/3004524)
    // and https://github.com/dotnet/coreclr/issues/430#issuecomment-85823468
    class HeapAllocStackAlloc
    {
        //[ParamsWithSteps(start:10, end:4010, step:500)]
        [ParamsWithSteps(start:6000, end:96000, step:10000)] // Duplicate 'ParamsWithSteps' attribute
        public long ArraySize = 0;

        [Benchmark]
        public int GetSquareHeapAlloc(IterationParams iteration)
        {
            int[] someNumbers = new int[ArraySize];
            int value = (int)(iteration.Count % ArraySize);
            for (int i = 0; i < someNumbers.Length; ++i)
            {
                someNumbers[i] = value;
            }

            return someNumbers[value];
        }

        //public unsafe int GetSquareStackAlloc(int value)
        //{
        //    int* someNumbers = stackalloc int[ArraySize];
        //    for (int i = 0; i < ArraySize; ++i)
        //    {
        //        someNumbers[i] = value;
        //    }

        //    return someNumbers[value];
        //}
    }
}
