using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MiniBench
{
    internal class Analyser
    {
        private readonly String benchmarkAttribute = "Benchmark";

        private readonly string[] allowedInjectedParamaters = new[]
            {
                "IterationParams",
                //"BenchmarkParams
            };

        internal IEnumerable<BenchmarkInfo> AnalyseBenchmark(SyntaxTree benchmarkCode, string filePrefix)
        {
            // TODO see if we need to get the semantic model for the code, not just the syntax one?
            // At the moment we're just doing a string match on the Attribute/Parameter type, so it's not completely robust!!
            // See https://joshvarty.wordpress.com/2014/10/30/learn-roslyn-now-part-7-introducing-the-semantic-model/
            // var compilation = CSharpCompilation.Create("MyCompilation",
            //        syntaxTrees: new[] { tree }, references: new[] { MetadataReference.CreateFromAssembly(typeof(object).Assembly) });
            // var model = compilation.GetSemanticModel(tree);

            // TODO error checking, in case the file doesn't have a Namespace, Class or any valid Methods!
            var @namespace = NodesOfType<NamespaceDeclarationSyntax>(benchmarkCode).FirstOrDefault();
            var namespaceName = @namespace.Name.ToString();
            // TODO we're not robust to having multiple classes in 1 file, we need to find the class that contains the [Benchmark] methods!!
            var @class = NodesOfType<ClassDeclarationSyntax>(benchmarkCode).FirstOrDefault();
            var className = @class.Identifier.ToString();
            var methods = NodesOfType<MethodDeclarationSyntax>(benchmarkCode);

            var fields = NodesOfType<FieldDeclarationSyntax>(@class.SyntaxTree);
            if (fields.Count > 0)
                Console.WriteLine("Fields:\n" + String.Join("\n", fields.Select(f => f.ToString() + " -> " + String.Join(", ", f.AttributeLists.SelectMany(atr => atr.Attributes)))) + "\n");
            var properties = NodesOfType<PropertyDeclarationSyntax>(@class.SyntaxTree);
            if (properties.Count > 0)
                Console.WriteLine("Properties:\n" + String.Join("", properties.Select(p => p.ToString() + " -> " + String.Join(", ", p.AttributeLists.SelectMany(atr => atr.Attributes)))) + "\n");

            PrintMethodDebuggingInfo(methods);

            var validMethods = methods.Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                                      .Where(m => m.AttributeLists.SelectMany(atrl => atrl.Attributes)
                                                                  .Any(atr => atr.Name.ToString() == benchmarkAttribute))
                                      .ToList();
            var benchmarkInfo = new List<BenchmarkInfo>(validMethods.Count);
            foreach (var method in validMethods)
            {
                var methodName = method.Identifier.ToString();
                // Can't have '.' or '-' in class names (which is where this gets used)
                var generatedClassName = string.Format("{0}_{1}_{2}_{3}",
                                                       filePrefix,
                                                       namespaceName.Replace('.', '_'),
                                                       className,
                                                       methodName);
                var fileName = string.Format(generatedClassName + ".cs");

                var generateBlackhole = ShouldGenerateBlackhole(method.ReturnType);
                var parametersToInject = method.ParameterList.Parameters
                                               .Where(p => allowedInjectedParamaters.Any(a => a == p.Type.ToString()))
                                               .Select(p => p.Type.ToString())
                                               .ToList();

                benchmarkInfo.Add(new BenchmarkInfo
                    {
                        NamespaceName = @namespaceName,
                        ClassName = className,
                        MethodName = methodName,
                        FileName = fileName,
                        GeneratedClassName = generatedClassName,

                        GenerateBlackhole = generateBlackhole,
                        ParametersToInject = parametersToInject,

                        // TODO Complete these 2!!!!
                        ParamsWithSteps = null,
                        ParamsFieldName = null,
                    });
            }

            return benchmarkInfo;
        }

        private bool ShouldGenerateBlackhole(TypeSyntax returnType)
        {
            // If the method returns void, double, etc, then the type will be "PredefinedTypeSyntax"
            var predefinedTypeSyntax = returnType as PredefinedTypeSyntax;
            if (predefinedTypeSyntax != null && predefinedTypeSyntax.Keyword.IsKind(SyntaxKind.VoidKeyword) == false)
                return true;

            // If the method returns DateTime, String, etc, then the type will be "IdentifierNameSyntax"
            var identifierNameSyntax = returnType as IdentifierNameSyntax;
            if (identifierNameSyntax != null && identifierNameSyntax.IsKind(SyntaxKind.VoidKeyword) == false)
                return true;

            // If we don't know, return false?
            return false;
        }

        private void PrintMethodDebuggingInfo(IEnumerable<MethodDeclarationSyntax> methods)
        {
            var methodInfo = methods.Select(m => new
            {
                Name = m.Identifier.ToString(),
                ReturnType = m.ReturnType,
                Blackhole = ShouldGenerateBlackhole(m.ReturnType),
                Attributes = String.Join(", ", m.AttributeLists.SelectMany(atrl => atrl.Attributes.Select(atr => atr.GetText()))),
                InjectedArgs = String.Join(", ", m.ParameterList.Parameters.Select(p => "\"" + p.GetText() + "\""))
                //InjectedArgs = String.Join(", ", m.ParameterList.Parameters.Select(p => p.Type + " " + p.Identifier))
            });
            var methodInfoText = methodInfo.Select(m =>
            {
                return String.Format("{0,30} - {1} - Blackhole={2}, Attributes = {3}, Parameters = {4}",
                                     m.Name,
                                     m.ReturnType.ToString().PadRight(10),
                                     m.Blackhole.ToString().PadRight(5),
                                     m.Attributes,
                                     m.InjectedArgs);
            });
            Console.WriteLine(String.Join("\n", methodInfoText));
        }

        private static IList<T> NodesOfType<T>(SyntaxTree tree)
        {
            return tree.GetRoot()
                       .DescendantNodes()
                       .OfType<T>()
                       .ToList();
        }
    }
}
