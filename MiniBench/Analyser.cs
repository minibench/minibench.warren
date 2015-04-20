using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MiniBench.Core;

namespace MiniBench
{
    /// <summary>
    /// This class analyses a SyntaxTree (coming from a single .cs file) and returns a list of all the 
    /// benchmarks contained in the file, along with all the relevant information (BenchmarkInfo)
    /// </summary>
    internal class Analyser
    {
        private readonly String benchmarkAttribute = "Benchmark";

        private readonly String paramsWithStepsAttribute = "ParamsWithSteps";

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
                Console.WriteLine("Processing: {0}.{1}", namespaceName, className);
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
            // TODO throw an error if there are multiple fields/properties with [ParamsWithSteps]
            // Currently we only allow 1 (at the moment the first one found is used and the rest ignored)
            var fields = @class.ChildNodes().OfType<FieldDeclarationSyntax>().ToList();
            if (fields.Count > 0)
            {
                var fieldsWithInfo = fields
                    .Where(f => f.AttributeLists.SelectMany(al => al.Attributes).Any(a => a.Name.ToString() == paramsWithStepsAttribute))
                    .Select(f =>
                    {
                        return f.Modifiers + " " +
                               f.Declaration.Type + " " +
                               String.Join(", ", f.Declaration.Variables.Select(v => v.Identifier)) + " -> " +
                               String.Join(", ", f.AttributeLists.SelectMany(atr => atr.Attributes));
                    }).ToList();
                if (fieldsWithInfo.Any())
                    Console.WriteLine("Fields:\n  " + String.Join("\n  ", fieldsWithInfo));

                foreach (var field in fields)
                {
                    // TODO, should we throw an error if [ParamsWithSteps] is applied to a field that has multiple variables (i.e. on one line)?
                    var variableToUse = field.Declaration.Variables.FirstOrDefault();
                    var variableName = variableToUse.Identifier.ToString();
                    var attribute = GetParamsWithStepsAttribute(variableName, field.AttributeLists);
                    if (attribute != null)
                        return Tuple.Create(attribute, variableName);
                }
            }

            var properties = @class.ChildNodes().OfType<PropertyDeclarationSyntax>().ToList();
            if (properties.Count > 0)
            {
                var propertiesWithInfo = properties
                    .Where(p => p.AttributeLists.SelectMany(al => al.Attributes).Any(a => a.Name.ToString() == paramsWithStepsAttribute))
                    .Select(p =>
                    {
                        return p.Modifiers + " " +
                               p.Type + " " +
                               p.Identifier + " " +
                               p.AccessorList + " -> " +
                               String.Join(", ",
                                           p.AttributeLists.SelectMany(atr => atr.Attributes));
                    }).ToList();
                if (propertiesWithInfo.Any())
                    Console.WriteLine("Properties:\n  " + String.Join("\n  ", propertiesWithInfo));

                foreach (var property in properties)
                {
                    var attribute = GetParamsWithStepsAttribute(property.Identifier.ToString(), property.AttributeLists);
                    if (attribute != null)
                        return Tuple.Create(attribute, property.Identifier.ToString());
                }
            }

            return Tuple.Create<ParamsWithStepsAttribute, String>(null, null);
        }

        private ParamsWithStepsAttribute GetParamsWithStepsAttribute(string fieldOrPropertyName, SyntaxList<AttributeListSyntax> attributeLists)
        {
            foreach (var attribute in attributeLists.SelectMany(attributeList =>
                            attributeList.Attributes.Where(attribute => attribute.Name.ToString() == paramsWithStepsAttribute)))
            {
                var arguments = (from argument in attribute.ArgumentList.Arguments
                                 select argument.Expression as LiteralExpressionSyntax
                                 into expression
                                 where expression != null && expression.Token.Value is int
                                 select (int) expression.Token.Value).ToList();

                const int expectedArgCount = 3;
                if (arguments.Count != expectedArgCount)
                {
                    var rawArgs = String.Join(", ", attribute.ArgumentList.Arguments.Select(
                        arg =>
                            {
                                var literalExpressionSyntax = arg.Expression as LiteralExpressionSyntax;
                                return literalExpressionSyntax != null
                                           ? ((int) (literalExpressionSyntax.Token.Value)).ToString(CultureInfo.InvariantCulture)
                                           : arg.Expression.ToString();
                            }));
                    Console.WriteLine("{0} -> {1} - Wrong number of args, Expected {2}, Got {3}",
                                      fieldOrPropertyName, rawArgs, expectedArgCount, arguments.Count);
                    continue;
                }

                return new ParamsWithStepsAttribute(arguments[0], arguments[1], arguments[2]);
            }

            return null;
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
