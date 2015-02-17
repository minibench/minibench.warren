﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MiniBench.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MiniBench
{
    internal class CodeGenerator
    {
        private readonly string rootFolder;
        private readonly IList<Tuple<string, string>> references;
        private readonly IList<string> sourceFiles;

        private readonly CSharpParseOptions parseOptions = 
            new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.CSharp2);

        private readonly Encoding defaultEncoding = Encoding.UTF8;

        internal CodeGenerator(string rootFolder, IList<string> sourceFiles, IList<Tuple<string, string>> references)
        {
            this.rootFolder = rootFolder;
            this.sourceFiles = sourceFiles;
            this.references = references;
        }

        internal void GenerateCode()
        {
            var outputDirectory = Environment.CurrentDirectory;
            var allSyntaxTrees = new List<SyntaxTree>(GenerateEmbeddedCode());
            foreach (var file in sourceFiles.Where(f => f.StartsWith("Properties\\") == false))
            {
                var code = File.ReadAllText(Path.Combine(rootFolder, file));
                var benchmarkTree = CSharpSyntaxTree.ParseText(code, options: parseOptions);

                // TODO error cheecking, in case the file doesn't have a Namespace, Class or any valid Methods!
                var @namespace = NodesOfType<NamespaceDeclarationSyntax>(benchmarkTree).FirstOrDefault();
                var namespaceName = @namespace.Name.ToString();
                var @class = NodesOfType<ClassDeclarationSyntax>(benchmarkTree).FirstOrDefault();
                var className = @class.Identifier.ToString();
                var allMethods = NodesOfType<MethodDeclarationSyntax>(benchmarkTree);

                allSyntaxTrees.Add(benchmarkTree);

                var generatedRunners = GenerateRunners(allMethods, namespaceName, className, outputDirectory);
                allSyntaxTrees.AddRange(generatedRunners);

                // TODO we should do this here instaead, so there is a launcher per benchmark
                //var generatedLauncher = GenerateLauncher(namespaceName, className, outputDirectory);
                //allSyntaxTrees.Add(generatedLauncher);
            }

            var generatedLauncher = GenerateLauncher("MiniBench.Demo", "SampleConstantFolding", outputDirectory);
            allSyntaxTrees.Add(generatedLauncher);

            CompileAndEmitCode(allSyntaxTrees, emitToDisk: true);
        }

        private List<SyntaxTree> GenerateRunners(IList<MethodDeclarationSyntax> methods, string namespaceName, string className, string outputDirectory)
        {
            var filePrefix = "Generated_Runner";
            var fileDeletionTimer = Stopwatch.StartNew();
            foreach (var existingGeneratedFile in Directory.EnumerateFiles(outputDirectory, filePrefix + "*"))
            {
                File.Delete(existingGeneratedFile);
            }
            fileDeletionTimer.Stop();
            Console.WriteLine("Took {0} ({1,5:N0}ms) - to delete existing files from disk", fileDeletionTimer.Elapsed, fileDeletionTimer.ElapsedMilliseconds);

            var modifiers = methods.Select(m => m.Modifiers).ToList();
            var benchmarkAttribute = typeof(BenchmarkAttribute).Name.Replace("Attribute", "");
            var validMethods = methods.Where(m => m.Modifiers.Any(mod => mod.CSharpKind() == SyntaxKind.PublicKeyword))
                                      .Where(m => m.AttributeLists.SelectMany(atrl => atrl.Attributes)
                                                                  .Any(atr => atr.Name.ToString() == benchmarkAttribute))
                                      .ToList();
            var generatedRunners = new List<SyntaxTree>(validMethods.Count);
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
                var outputFileName = Path.Combine(outputDirectory, fileName);

                var codeGenTimer = Stopwatch.StartNew();
                var generatedBenchmark = BenchmarkTemplate.ProcessCodeTemplates(namespaceName, className, methodName, generatedClassName);
                var generatedRunnerTree = CSharpSyntaxTree.ParseText(generatedBenchmark, options: parseOptions, path: outputFileName, encoding: defaultEncoding);
                generatedRunners.Add(generatedRunnerTree);
                codeGenTimer.Stop();
                Console.WriteLine("Took {0} ({1,5:N0}ms) - to generate CSharp Syntax Tree", codeGenTimer.Elapsed, codeGenTimer.ElapsedMilliseconds);

                var fileWriteTimer = Stopwatch.StartNew();
                File.WriteAllText(outputFileName, generatedRunnerTree.GetRoot().ToFullString(), encoding: defaultEncoding);
                fileWriteTimer.Stop();
                Console.WriteLine("Generated file: " + fileName);
                Console.WriteLine("Took {0} ({1,5:N0}ms) - to write file to disk", fileWriteTimer.Elapsed, fileWriteTimer.ElapsedMilliseconds);
            }
            return generatedRunners;
        }

        private SyntaxTree GenerateLauncher(string namespaceName, string className, string outputDirectory)
        {
            var generatedLauncher = BenchmarkTemplate.ProcessLauncherTemplate(namespaceName, className);
            var fileName = "Generated_Launcher.cs";
            var outputFileName = Path.Combine(outputDirectory, fileName);
            var generatedLauncherTree = CSharpSyntaxTree.ParseText(generatedLauncher, options: parseOptions, path: outputFileName, encoding: defaultEncoding);
            File.WriteAllText(outputFileName, generatedLauncherTree.GetRoot().ToFullString(), encoding: defaultEncoding);
            Console.WriteLine("Generated file: " + fileName);

            return generatedLauncherTree;
        }

        private List<SyntaxTree> GenerateEmbeddedCode()
        {
            // TODO Maybe make this list automatic, i.e. search for all Embedded Resources in the "MiniBench.Core" namespace?
            var embeddedCodeFiles = new[] 
                {
                    "BenchmarkAttribute.cs", "CategoryAttribute.cs", 
                    "IBenchmarkTarget.cs", "BenchmarkResult.cs", 
                    "Options.cs", "OptionsBuilder.cs", "Runner.cs",
                };
            var embeddedCodeTrees = new List<SyntaxTree>();
            foreach (var codeFile in embeddedCodeFiles)
            {
                var codeText = GetEmbeddedResource("MiniBench.Core." + codeFile);
                var codeTree = CSharpSyntaxTree.ParseText(codeText, options: parseOptions);
                embeddedCodeTrees.Add(codeTree);
            }

            return embeddedCodeTrees;
        }

        private void CompileAndEmitCode(List<SyntaxTree> allSyntaxTrees, bool emitToDisk)
        {
            // TODO Maybe re-write this using the Fluent-API, as shown here http://roslyn.codeplex.com/discussions/541557
            var compilationOptions = new CSharpCompilationOptions(
                                            outputKind: OutputKind.ConsoleApplication,
                                            mainTypeName: "MiniBench.Benchmarks.Program",
                                            optimizationLevel: OptimizationLevel.Release);
            // TODO decide if this is a good idea or not?? We are re-writing over the top of the file that VS has just built!
            // But it does mean that you can use the Test Runner in VS to run the Integration Tests :-)
            var generatedCodeName = "MiniBench.Demo"; // "Benchmark";

            // One call here will be sloooowww (probably Create() or Emit()), because it causes a load/JIT of certain parts of Roslyn
            // see https://roslyn.codeplex.com/discussions/573503 for a full explanation (JITting of Roslyn dll's is the main cause)

            var compilationTimer = Stopwatch.StartNew();
            var compilation = CSharpCompilation.Create(generatedCodeName, allSyntaxTrees, GetRequiredReferences(), compilationOptions);
            compilationTimer.Stop();
            Console.WriteLine("Took {0} ({1,5:N0}ms) - to create the CSharpCompilation", compilationTimer.Elapsed, compilationTimer.ElapsedMilliseconds);

            if (emitToDisk)
            {
                Console.WriteLine("\nCurrent directory: " + Environment.CurrentDirectory);
                var codeEmitToDiskTimer = Stopwatch.StartNew();
                var emitToDiskResult = compilation.Emit(outputPath: generatedCodeName + ".exe",
                                                        pdbPath: generatedCodeName + ".pdb",
                                                        xmlDocumentationPath: generatedCodeName + ".xml");
                codeEmitToDiskTimer.Stop();
                Console.WriteLine("Emit to DISK Success: {0}\n  {1}", emitToDiskResult.Success, string.Join("\n  ", emitToDiskResult.Diagnostics));
                Console.WriteLine("Took {0} ({1,5:N0}ms) - to emit generated code to DISK", codeEmitToDiskTimer.Elapsed, codeEmitToDiskTimer.ElapsedMilliseconds);
            }
        }

        private IList<MetadataReference> GetRequiredReferences()
        {
            var standardReferences = new List<MetadataReference>
                { 
                    // TODO need to get a reference to mscorlib v 2.0 here, 
                    // can't use "typeof(String).Assembly" as that's v 4.0 (because MiniBench itself is 4.0)
                    //  warning CS1701: Assuming assembly reference 
                    //'mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' used by 'MiniBench.Core' matches identity 
                    //'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' of 'mscorlib', you may need to supply runtime policy
                    // see http://source.roslyn.codeplex.com/#Roslyn.Compilers.CSharp.Symbol.UnitTests/Compilation/ReferenceManagerTests.cs
                    // and http://source.roslyn.codeplex.com/#Roslyn.Test.Utilities/TestBase.cs,d7b20a77e5912080
                    // and http://source.roslyn.codeplex.com/#Roslyn.Compilers.CSharp.Symbol.UnitTests/Compilation/ReferenceManagerTests.cs,a90d68f18e804c1a,references

                    //MetadataReference.CreateFromAssembly(typeof(String).Assembly),
                    //MetadataReference.CreateFromAssembly(Assembly.Load("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                    MetadataReference.CreateFromAssembly(Assembly.LoadFile(@"C:\Windows\Microsoft.NET\Framework64\v2.0.50727\mscorlib.dll")),
                    //MetadataReference.CreateFromAssembly(Assembly.LoadFrom(@"C:\Windows\Microsoft.NET\Framework64\v2.0.50727\mscorlib.dll")),
                    //MetadataReference.CreateFromAssembly(Assembly.LoadFrom(@"C:\Windows\Microsoft.NET\Framework\v2.0.50727\mscorlib.dll")),

                    // In here we include any other parts of the .NET framework that we need
                    MetadataReference.CreateFromAssembly(typeof(System.Diagnostics.Stopwatch).Assembly),
                };

            // Now add the references we need from the .csproj file
            foreach (var reference in references)
            {
                standardReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rootFolder, reference.Item2)));
            }

            return standardReferences;
        }

        private static string GetEmbeddedResource(string resourceFullPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceFullPath))
            using (StreamReader reader = new StreamReader(stream))
            {
                string result = reader.ReadToEnd();
                return result;
            }
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
