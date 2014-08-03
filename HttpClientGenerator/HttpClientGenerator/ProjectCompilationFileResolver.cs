using System;
using System.Linq;
using System.Xml.Linq;

namespace HttpClientGenerator
{
    internal class ProjectCompilationFileResolver
    {
        private readonly string _projectPath;

        public ProjectCompilationFileResolver(string projectPath)
        {
            _projectPath = projectPath;
        }

        public string[] ResolveCompilationFiles()
        {
            var doc = XDocument.Load(_projectPath);
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
            var x = doc.Descendants(ns + "Compile");
            var inputFiles = x.Select(
                n => n.Attribute("Include")
                      .Value);
            var files = inputFiles.ToArray();
            Console.WriteLine("Files: {0}", string.Join(", ", files));
            return files;
        }
    }
}