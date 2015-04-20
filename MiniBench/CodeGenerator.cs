using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
        private readonly ProjectSettings projectSettings;

        private readonly CSharpParseOptions parseOptions;

        private readonly Encoding defaultEncoding = Encoding.UTF8;

        private readonly String filePrefix = "Generated_Runner";
        private readonly String launcherFileName = "Generated_Launcher.cs";

        internal CodeGenerator(ProjectSettings projectSettings)
        {
            this.projectSettings = projectSettings;

            // We don't want to target .NET 2.0 only LANGUAGE features, otherwise we can't use all the nice compiler-only stuff
            // like var, auto-properties, named arguments, etc. The main thing is that when we build the Benchmark .exe/.dll, 
            // we need to TARGET the correct Runtime Framework Version, which is either .NET 2.0 or 4.0.
            parseOptions = new CSharpParseOptions(kind: SourceCodeKind.Regular, languageVersion: LanguageVersion.CSharp4);
        }

        internal void GenerateCode()
        {
            var outputDirectory = Environment.CurrentDirectory;
            var generatedCodeDirectory = Path.Combine(outputDirectory, "GeneratedCode");
            Directory.CreateDirectory(generatedCodeDirectory);
            var fileDeletionTimer = Stopwatch.StartNew();
            foreach (var existingGeneratedFile in Directory.EnumerateFiles(generatedCodeDirectory, filePrefix + "*"))
            {
                File.Delete(existingGeneratedFile);
            }
            fileDeletionTimer.Stop();
            Console.WriteLine("\nTook {0} ({1,7:N2} ms) - to delete existing files from disk\n", fileDeletionTimer.Elapsed, fileDeletionTimer.Elapsed.TotalMilliseconds);

            var allSyntaxTrees = new List<SyntaxTree>(GenerateEmbeddedCode());
            var analyser = new Analyser();
            foreach (var file in projectSettings.SourceFiles.Where(f => f.StartsWith("Properties\\") == false))
            {
                Console.WriteLine("Processing file: " + file);
                var filePath = Path.Combine(projectSettings.RootFolder, file);
                var code = File.ReadAllText(filePath);
                var benchmarkTree = CSharpSyntaxTree.ParseText(code, options: parseOptions, path: filePath, encoding: defaultEncoding);

                var analysisTimer = Stopwatch.StartNew();
                var benchmarkInfo = analyser.AnalyseBenchmark(benchmarkTree, filePrefix);
                analysisTimer.Stop();
                Console.WriteLine("Took {0} ({1,7:N2} ms) - to analyse the benchmark code", analysisTimer.Elapsed, analysisTimer.Elapsed.TotalMilliseconds);

                allSyntaxTrees.Add(benchmarkTree);

                var generatedRunners = GenerateRunners(benchmarkInfo, generatedCodeDirectory);
                allSyntaxTrees.AddRange(generatedRunners);
            }

            var generatedLauncher = GenerateLauncher(generatedCodeDirectory);
            allSyntaxTrees.Add(generatedLauncher);

            CompileAndEmitCode(allSyntaxTrees);
        }

        private IEnumerable<SyntaxTree> GenerateRunners(IEnumerable<BenchmarkInfo> benchmarkInfo, string outputDirectory)
        {
            var generatedRunners = new List<SyntaxTree>(benchmarkInfo.Count());
            foreach (var info in benchmarkInfo)
            {
                var codeGenTimer = Stopwatch.StartNew();
                var outputFileName = Path.Combine(outputDirectory, info.FileName);
                var generatedBenchmark = BenchmarkTemplate.ProcessCodeTemplates(info);
                var generatedRunnerTree = CSharpSyntaxTree.ParseText(generatedBenchmark, options: parseOptions, path: outputFileName, encoding: defaultEncoding);
                generatedRunners.Add(generatedRunnerTree);
                codeGenTimer.Stop();
                Console.WriteLine("Took {0} ({1,7:N2} ms) - to generate CSharp Syntax Tree", codeGenTimer.Elapsed, codeGenTimer.Elapsed.TotalMilliseconds);

                var fileWriteTimer = Stopwatch.StartNew();
                File.WriteAllText(outputFileName, generatedRunnerTree.GetRoot().ToFullString(), encoding: defaultEncoding);
                fileWriteTimer.Stop();
                Console.WriteLine("Took {0} ({1,7:N2} ms) - to write file to disk", fileWriteTimer.Elapsed, fileWriteTimer.Elapsed.TotalMilliseconds);
                Console.WriteLine("Generated file: {0}\n", info.FileName);
            }
            return generatedRunners;
        }

        private SyntaxTree GenerateLauncher(string outputDirectory)
        {
            var generatedLauncher = LauncherTemplate.ProcessLauncherTemplate();
            var outputFileName = Path.Combine(outputDirectory, launcherFileName);
            var codeGenTimer = Stopwatch.StartNew();
            var generatedLauncherTree = CSharpSyntaxTree.ParseText(generatedLauncher, options: parseOptions, path: outputFileName, encoding: defaultEncoding);
            codeGenTimer.Stop();
            Console.WriteLine("Took {0} ({1,7:N2} ms) - to generate CSharp Syntax Tree", codeGenTimer.Elapsed, codeGenTimer.Elapsed.TotalMilliseconds);

            var fileWriteTimer = Stopwatch.StartNew();
            File.WriteAllText(outputFileName, generatedLauncherTree.GetRoot().ToFullString(), encoding: defaultEncoding);
            fileWriteTimer.Stop();
            Console.WriteLine("Took {0} ({1,7:N2} ms) - to write file to disk", fileWriteTimer.Elapsed, fileWriteTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Generated file: " + launcherFileName);

            return generatedLauncherTree;
        }

        private IEnumerable<SyntaxTree> GenerateEmbeddedCode()
        {
            var embeddedCodeTrees = new List<SyntaxTree>();
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var codeFile in assembly.GetManifestResourceNames())
            {
                if (codeFile.StartsWith("MiniBench.Core.") == false &&
                    codeFile.StartsWith("MiniBench.Profiling.") == false)
                    continue;

                using (Stream stream = assembly.GetManifestResourceStream(codeFile))
                using (var reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    // By adding a "virtual" path we can match up the errors/warnings (in the VS Output Window) with the embedded resource .cs file
                    var codeTree = CSharpSyntaxTree.ParseText(result, options: parseOptions, path: codeFile, encoding: defaultEncoding);
                    embeddedCodeTrees.Add(codeTree);
                }
            }

            return embeddedCodeTrees;
        }

        private void CompileAndEmitCode(IEnumerable<SyntaxTree> allSyntaxTrees)
        {
            // As we're adding our own "main" function, we always Compile as OutputKind.ConsoleApplication, regardless of the actual extension (dll/exe)
            var compilationOptions = new CSharpCompilationOptions(
                                            outputKind: OutputKind.ConsoleApplication,
                                            mainTypeName: "MiniBench.Benchmarks.Program",
                                            optimizationLevel: OptimizationLevel.Release,
                                            allowUnsafe: projectSettings.AllowUnsafe);

            // One call here will be sloooowww (probably Create() or Emit()), because it causes a load/JIT of certain parts of Roslyn
            // see https://roslyn.codeplex.com/discussions/573503 for a full explanation (JITting of Roslyn dll's is the main cause)

            var compilationTimer = Stopwatch.StartNew();
            var compilation = CSharpCompilation.Create(projectSettings.OutputFileName, allSyntaxTrees, GetRequiredReferences(), compilationOptions);
            compilationTimer.Stop();
            Console.WriteLine("\nTook {0} ({1,7:N2} ms) - to create the CSharpCompilation", compilationTimer.Elapsed, compilationTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("\nCurrent directory: " + Environment.CurrentDirectory);

            // TODO fix this IOException (happens if the file is still being used whilst we are trying to "re-write" it)
            //  Unhandled Exception: System.IO.IOException: The process cannot access the file '....MiniBench.Demo.dll' because it is being used by another process.

            // TODO we should probably emit to a .temp file, than only if it's successful copy that over the top of the existing file (and delete the .temp file)
            // that way, if something goes wrong the original binaries are left in-tact and we never emit invalid files

            var codeEmitToDiskTimer = Stopwatch.StartNew();
            var emitToDiskResult = compilation.Emit(outputPath: projectSettings.OutputFileName + projectSettings.OutputFileExtension,
                                                    pdbPath: projectSettings.OutputFileName + ".pdb",
                                                    xmlDocPath: projectSettings.OutputFileName + ".xml");
            codeEmitToDiskTimer.Stop();
            Console.WriteLine("Took {0} ({1,7:N2} ms) - to emit generated code to DISK", codeEmitToDiskTimer.Elapsed, codeEmitToDiskTimer.Elapsed.TotalMilliseconds);
            Console.WriteLine("Emit to DISK Success: {0}", emitToDiskResult.Success);
            if (emitToDiskResult.Diagnostics.Length > 0)
            {
                Console.WriteLine("\nCompilation Warnings:\n\t{0}\n", string.Join("\n\t", emitToDiskResult.Diagnostics));
            }
        }

        private IEnumerable<MetadataReference> GetRequiredReferences()
        {
            var standardReferences = new List<MetadataReference>(16);
            if (projectSettings.TargetFrameworkVersion == LanguageVersion.CSharp2 ||
                projectSettings.TargetFrameworkVersion == LanguageVersion.CSharp3)
            {
                // We have to read the dll's from disk as a Stream and create a MetadataReference from that.
                // If we use MetadataReference.CreateFromAssembly(..) the .NET 4.0 versions are used instead.
                var runtimeDlls = new[]
                    {
                        // TODO C:\Windows\Microsoft.NET\Framework64 or just \Framework ?!?
                        @"C:\Windows\Microsoft.NET\Framework\v2.0.50727\mscorlib.dll",
                        @"C:\Windows\Microsoft.NET\Framework\v2.0.50727\System.dll"
                    };

                foreach (var runtimeDll in runtimeDlls)
                {
                    using (var fileStream = File.OpenRead(runtimeDll))
                    {
                        standardReferences.Add(MetadataReference.CreateFromStream(fileStream, filePath: runtimeDll));
                    }
                }
            }
            else
            {
                // As MiniBench.exe runs as a .NET 4.0 (or 4.5) process (due to the Roslyn dependancy)
                // We can just get the .NET 4.0 runtimes components in the normal way
                // Using typeof(..) means we get the best match, for instance from the GAC

                // This pulls in mscorlib.dll 
                standardReferences.Add(MetadataReference.CreateFromAssembly(typeof(String).Assembly));
                // This pulls in System.dll
                standardReferences.Add(MetadataReference.CreateFromAssembly(typeof(Stopwatch).Assembly));
                // This pulls in System.Core, i.e. the stuff you need for LINQ
                standardReferences.Add(MetadataReference.CreateFromAssembly(typeof(System.Linq.Enumerable).Assembly));
            }

            // Now add the references we need from the .csproj file
            foreach (var reference in projectSettings.References)
            {
                // TODO we need to handle references that don't have a "HintPath", i.e. things like:
                // <Reference Include="System" />
                // <Reference Include="System.Data" />
                // <Reference Include="System.Xml" />
                // TODO At the moment we only deal with ones like this:
                // <Reference Include="xunit">
                //     <HintPath>..\packages\xunit.1.9.2\lib\net20\xunit.dll</HintPath>
                // </Reference>

                // TODO work out how to get rid of the following Warning (that appears in the VS Output Window)
                // warning CS1701: Assuming assembly reference 'mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' used by 'xunit' 
                //                            matches identity 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089' of 'mscorlib', 
                //                 you may need to supply runtime policy

                var metadataReference = MetadataReference.CreateFromFile(Path.GetFullPath(Path.Combine(projectSettings.RootFolder, reference.Item2)));
                standardReferences.Add(metadataReference);
            }

            Console.WriteLine("\nAdding References:\n\t" + String.Join("\n\t", standardReferences.Select(r => r.Display)));

            return standardReferences;
        }
    }
}
