using System;

namespace MiniBench.Core
{
    /// <summary>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class ParamsWithStepsAttribute : Attribute
    {
        public int Start { get; private set; }
        public int End { get; private set; }
        public int Step { get; private set; }

        public ParamsWithStepsAttribute(int start, int end, int step)
        {
            Start = start;
            End = end;
            Step = step;
        }
    }
}
