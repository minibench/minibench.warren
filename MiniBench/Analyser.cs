using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MiniBench.Core;

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
            var @namespace = benchmarkCode.GetRoot()
                                          .DescendantNodes()
                                          .OfType<NamespaceDeclarationSyntax>()
                                          .FirstOrDefault();
            var namespaceName = @namespace.Name.ToString();

            var benchmarkInfo = new List<BenchmarkInfo>();
            foreach (var @class in @namespace.ChildNodes().OfType<ClassDeclarationSyntax>())
            {
                var className = @class.Identifier.ToString();
                var methods = @class.ChildNodes().OfType<MethodDeclarationSyntax>().ToList();
                var paramInfo = GetParamInfo(@class);
                PrintMethodDebuggingInfo(methods);

                foreach (var method in methods.Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                                              .Where(m => m.AttributeLists.SelectMany(atrl => atrl.Attributes)
                                                                      .Any(atr => atr.Name.ToString() == benchmarkAttribute)))
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
                            ParamsWithSteps = paramInfo.Item1,
                            ParamsFieldName = paramInfo.Item2,
                        });
                }
            }

            return benchmarkInfo;
        }

        private Tuple<ParamsWithStepsAttribute, String> GetParamInfo(ClassDeclarationSyntax @class)
        {
            var fields = @class.ChildNodes().OfType<FieldDeclarationSyntax>().ToList();
            if (fields.Count > 0)
                Console.WriteLine("Fields:\n  " +
                                  String.Join("\n  ",
                                              fields.Select(
                                                  f =>
                                                  f.ToString() + " -> " +
                                                  String.Join(", ", f.AttributeLists.SelectMany(atr => atr.Attributes)))));

            var properties = @class.ChildNodes().OfType<PropertyDeclarationSyntax>().ToList();
            if (properties.Count > 0)
            {
                Console.WriteLine("Properties:\n  " +
                                  String.Join("\n  ",
                                              properties.Select(
                                                  p =>
                                                  p.Modifiers + " " + p.Type + " " + p.Identifier + " " + p.AccessorList +
                                                  " -> " +
                                                  String.Join(", ", p.AttributeLists.SelectMany(atr => atr.Attributes)))));

                foreach (var property in properties)
                {
                    foreach (var attribute in property.AttributeLists
                                .SelectMany(attributeList => 
                                            attributeList.Attributes.Where(attribute => attribute.Name.ToString() == "ParamsWithSteps")))
                    {
                        var args = String.Join(", ",
                                               attribute.ArgumentList.Arguments.Select(
                                                   arg => (int) ((arg.Expression as LiteralExpressionSyntax).Token.Value)));
                        Console.WriteLine("MATCH: " + attribute.Name + " -> " + args);
                        
                        var arguments = (from argument in attribute.ArgumentList.Arguments
                                         select argument.Expression as LiteralExpressionSyntax
                                         into expression
                                         where expression != null && expression.Token.Value is int
                                         select (int) expression.Token.Value).ToList();

                        if (arguments.Count != 3) 
                            continue;

                        Console.WriteLine("RESULT: " + property.Identifier.ToString() + " -> " + String.Join(", ", arguments));
                        return
                            Tuple.Create(
                                new ParamsWithStepsAttribute(arguments[0], arguments[1], arguments[2]),
                                property.Identifier.ToString());
                    }
                }
            }

            return Tuple.Create<ParamsWithStepsAttribute, String>(null, null);
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
    }
}
