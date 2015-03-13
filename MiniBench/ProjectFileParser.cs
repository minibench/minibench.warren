﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace MiniBench
{
    public class ProjectSettings
    {
        public string RootFolder { get; private set; }

        public string OutputFileName { get; private set; }
        
        public string OutputFileExtension { get; private set; }

        public IList<String> SourceFiles { get; private set; }

        public IList<Tuple<String, String>> References { get; private set; }
        
        public ProjectSettings(string rootFolder, string outputFileName, string outputFileExtension, 
                               IList<String> sourceFiles, IList<Tuple<String, String>> references)
        {
            RootFolder = rootFolder;
            OutputFileName = outputFileName;
            OutputFileExtension = outputFileExtension;
            References = references;
            SourceFiles = sourceFiles;
        }       
    }

    internal class ProjectFileParser
    {
        internal ProjectSettings ParseProjectFile(string csprojPath)
        {
            // From http://stackoverflow.com/questions/4649989/reading-a-csproj-file-in-c-sharp/4650090#4650090
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.Load(csprojPath);

            XmlNamespaceManager mgr = new XmlNamespaceManager(xmldoc.NameTable);
            mgr.AddNamespace("x", "http://schemas.microsoft.com/developer/msbuild/2003");

            Console.WriteLine("Reading from " + csprojPath);

            return new ProjectSettings(
                Path.GetDirectoryName(csprojPath),
                GetOutputFileName(xmldoc, mgr),
                GetOutputFileExtension(xmldoc, mgr),
                GetSourceFiles(xmldoc, mgr),
                GetReferences(xmldoc, mgr));
        }

        private String GetOutputFileName(XmlDocument xmldoc, XmlNamespaceManager mgr)
        {
            XmlNode item = xmldoc.SelectSingleNode("//x:AssemblyName", mgr);
            Console.WriteLine("AssemblyName: " + (item != null ? item.InnerText : "<NULL>"));
            if (item != null)
            {
                return item.InnerText;
            }

            return "Unknown";

        }

        private String GetOutputFileExtension(XmlDocument xmldoc, XmlNamespaceManager mgr)
        {
            XmlNode item = xmldoc.SelectSingleNode("//x:OutputType", mgr);
            Console.WriteLine("OutputType: " + (item != null ? item.InnerText : "<NULL>"));
            if (item != null)
            {
                if (item.InnerText == "Exe" || item.InnerText == "WinExe")
                    return ".exe";
                else if (item.InnerText == "Library")
                    return ".dll";
            }

            return ".Unknown";
        }

        private IList<String> GetSourceFiles(XmlDocument xmldoc, XmlNamespaceManager mgr)
        {
            var sourceFiles = new List<String>();
            Console.WriteLine("Source Files:");
            foreach (XmlNode item in xmldoc.SelectNodes("//x:Compile", mgr))
            {
                var includeAttrribute = item.Attributes["Include"];
                if (includeAttrribute == null)
                    continue;

                Console.WriteLine("\t" + includeAttrribute.Value);
                sourceFiles.Add(includeAttrribute.Value);
            }
            return sourceFiles;
        }

        private IList<Tuple<String, String>> GetReferences(XmlDocument xmldoc, XmlNamespaceManager mgr)
        {
            var references = new List<Tuple<String, String>>();
            // Why doesn't "//x:ItemGroup/Reference" work, to find Reference Nodes under ItemGroup nodes?
            Console.WriteLine("References:");
            foreach (XmlNode item in xmldoc.SelectNodes("//x:Reference", mgr))
            {
                var includeAttrribute = item.Attributes["Include"];
                if (includeAttrribute == null || item.ChildNodes.Count == 0)
                    continue;

                foreach (XmlNode childNode in item.ChildNodes)
                {
                    Console.WriteLine("\t" + includeAttrribute.Value + " " + childNode.InnerText);
                    references.Add(Tuple.Create(includeAttrribute.Value, childNode.InnerText));
                }
            }
            return references;
        }
    }
}
