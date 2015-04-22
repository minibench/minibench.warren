using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniBench
{
    /// <summary>
    /// This class analyses a SyntaxTree (coming from a single .cs file) and returns a list of all the 
    /// benchmarks contained in the file, along with all the relevant information (BenchmarkInfo)
    /// </summary>
    internal class Analyser
    {
        private readonly String benchmarkAttribute = "Benchmark";
        private readonly String setupAttribute = "Setup";

        private readonly string[] allowedInjectedParamaters = new[]
            {
                "IterationParams",
                //"BenchmarkParams
            };

        internal IList<BenchmarkInfo> AnalyseBenchmark(SyntaxTree benchmarkCode, string filePrefix)
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
            var paramsAnalyser = new ParamsAttributeAnalyser();
            foreach (var @class in @namespace.ChildNodes().OfType<ClassDeclarationSyntax>())
            {
                var className = @class.Identifier.ToString();
                Console.WriteLine("Processing: {0}.{1}", namespaceName, className);
                var methods = @class.ChildNodes().OfType<MethodDeclarationSyntax>().ToList();
                PrintDebuggingInfoForMethods(methods);

                var benchmarkMethods = TryGetBenchmarkMethodsThrowIfInvalid(@class, methods);
                var paramInfo = paramsAnalyser.TryGetParamInfoThrowIfInvalid(@class);
                var setupMethod = TryGetSetupMethodThrowIfInvalid(methods);
                foreach (var method in benchmarkMethods)
                {
                    var methodName = method.Identifier.Text;
                    // Can't have '.' or '-' in class names (which is where this gets used)
                    var generatedClassName = string.Format("{0}_{1}_{2}_{3}",
                                                           filePrefix,
                                                           namespaceName.Replace('.', '_'),
                                                           className,
                                                           methodName);
                    var fileName = string.Format(generatedClassName + ".cs");
                    var generateBlackhole = ShouldGenerateBlackhole(method.ReturnType);
                    var parametersToInject = TryGetParametersThrowIfInvalid(methodName, method.ParameterList);
                    benchmarkInfo.Add(new BenchmarkInfo
                        {
                            NamespaceName = namespaceName,
                            ClassName = className,
                            MethodName = methodName,
                            FileName = fileName,
                            GeneratedClassName = generatedClassName,
                            GenerateBlackhole = generateBlackhole,
                            ParametersToInject = parametersToInject,
                            ParamsFieldName = paramInfo.Item1,
                            Params = paramInfo.Item2,
                            ParamsWithSteps = paramInfo.Item3,
                            SetupMethod = setupMethod != null ? setupMethod.Identifier.Text : null
                        });
                }
            }

            return benchmarkInfo;
        }

        private IEnumerable<MethodDeclarationSyntax> TryGetBenchmarkMethodsThrowIfInvalid(
            ClassDeclarationSyntax @class, IEnumerable<MethodDeclarationSyntax> methods)
        {
            var benchmarkMethods = methods.Where(m => m.AttributeLists.SelectMany(atrl => atrl.Attributes)
                                                       .Any(atr => atr.Name.ToString() == benchmarkAttribute))
                                          .ToList();

            if (benchmarkMethods.Count > 0 && PublicOrInternal(@class.Modifiers) == false)
            {
                var msg =
                    String.Format(
                        "Classes containing methods annotated with [{0}] must be public or internal, Class: {1} is {2}",
                        benchmarkAttribute, @class.Identifier.Text, String.Join(", ", @class.Modifiers));
                throw new InvalidOperationException(msg);
            }

            foreach (var method in benchmarkMethods)
            {
                if (PublicOrInternal(method.Modifiers) == false)
                {
                    var msg =
                        String.Format("Methods annotated with [{0}] must be public or internal, Method: {1} is {2}",
                                      benchmarkAttribute, method.Identifier.Text, String.Join(", ", method.Modifiers));
                    throw new InvalidOperationException(msg);
                }
            }

            return benchmarkMethods;
        }

        private MethodDeclarationSyntax TryGetSetupMethodThrowIfInvalid(IEnumerable<MethodDeclarationSyntax> methods)
        {
            var setupMethods = methods.Where(m => m.AttributeLists.SelectMany(atrl => atrl.Attributes)
                                                       .Any(atr => atr.Name.ToString() == setupAttribute))
                                          .ToList();

            if (setupMethods.Count > 1)
            {
                var msg =
                    String.Format(
                        "Only one method can be annotated with [{0}], Methods: {1}",
                        setupAttribute, String.Join(", ", setupMethods.Select(m => m.Identifier.Text)));
                throw new InvalidOperationException(msg);
            }

            var setupMethod = setupMethods.FirstOrDefault();
            if (setupMethod == null)
            {
                // Having a [Setup] method is optional, so just exit here
                return null;
            }

            if (PublicOrInternal(setupMethod.Modifiers) == false)
            {
                var msg =
                    String.Format("Methods annotated with [{0}] must be public or internal, Method: {1} is {2}",
                                    setupAttribute, setupMethod.Identifier.Text, String.Join(", ", setupMethod.Modifiers));
                throw new InvalidOperationException(msg);
            }

            var parameters = setupMethod.ParameterList.Parameters;
            if (parameters.Count > 0)
            {
                var msg =
                    String.Format("Methods annotated with [{0}] must have no parameters, Method: {1} has {2}",
                                    setupAttribute, setupMethod.Identifier.Text, String.Join(", ", parameters));
                throw new InvalidOperationException(msg);
            }

            return setupMethod;
        }

        private IEnumerable<string> TryGetParametersThrowIfInvalid(string methodName, ParameterListSyntax parameterList)
        {
            var allParamsValid = parameterList.Parameters
                           .All(p => allowedInjectedParamaters.Any(a => a == p.Type.ToString()));
            if (allParamsValid == false)
            {
                var msg = String.Format("Methods annotated with [{0}] can only accept allowed parameters ({1}), Method: {2} has parameters: {3}",
                                        benchmarkAttribute, String.Join(", ", allowedInjectedParamaters),
                                        methodName, String.Join(", ", parameterList.Parameters));
                throw new InvalidOperationException(msg);
            }

            var parametersToInject = parameterList.Parameters
                                           .Where(p => allowedInjectedParamaters.Any(a => a == p.Type.ToString()))
                                           .Select(p => p.Type.ToString())
                                           .ToList();
            return parametersToInject;
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

        private bool PublicOrInternal(SyntaxTokenList modifiers)
        {
            // Need to have either an explicit "public" or "internal" modified 
            // OR
            // no modifie, as this implies internal (which is the default);
            return modifiers.Any(
                        m => m.IsKind(SyntaxKind.PublicKeyword) ||
                        m.IsKind(SyntaxKind.InternalKeyword)) ||
                   modifiers.Count == 0;
        }

        private void PrintDebuggingInfoForMethods(IEnumerable<MethodDeclarationSyntax> methods)
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
