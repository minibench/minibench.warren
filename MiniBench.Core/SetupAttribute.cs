using System;

namespace MiniBench.Core
{
    /// <summary>
    /// Indicates that the method is a Setup method that is to be 
    /// called just before a run on Benchmark invocations
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class SetupAttribute : Attribute
    {
    }
}
