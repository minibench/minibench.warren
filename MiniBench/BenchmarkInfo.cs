using System.Collections.Generic;
using MiniBench.Core;

namespace MiniBench
{
    internal class BenchmarkInfo
    {
        public string NamespaceName { get; set; }
        public string ClassName { get; set; }
        public string MethodName { get; set; }

        public string FileName { get; set; }
        public string GeneratedClassName { get; set; }

        public bool GenerateBlackhole { get; set; }

        public IEnumerable<string> ParametersToInject { get; set; }

        public string ParamsFieldName { get; set; }
        public ParamsAttribute Params { get; set; }
        public ParamsWithStepsAttribute ParamsWithSteps { get; set; }
    }
}