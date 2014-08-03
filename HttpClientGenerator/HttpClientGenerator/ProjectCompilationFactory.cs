using System.IO;
using System.Linq;
using System.Web.Http;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace HttpClientGenerator
{
    internal class ProjectCompilationFactory
    {
        private readonly string _projectPath;
        private readonly ReferenceCache _referenceCache;
        private readonly ProjectCompilationFileResolver _fileResolver;

        public ProjectCompilationFactory(string projectPath, ReferenceCache referenceCache)
        {
            _projectPath = projectPath;
            _referenceCache = referenceCache;
            _fileResolver = new ProjectCompilationFileResolver(projectPath);
        }

        private MetadataFileReference[] BuildAssemblyReferences()
        {
            return
                new[]
                {
                    _referenceCache.MSCoreLib,
                    _referenceCache.SystemRuntime,
                    _referenceCache.AssemblyReferenceForType<ApiController>(), 
                    _referenceCache.ForAssemblyLocation(@"C:\pf\Stash\aco\Composite\Core\bin\Debug\PF.Aco.Entities.dll")
                };
        }

        public CSharpCompilation CreateCompilation()
        {
            string[] inputFiles = _fileResolver.ResolveCompilationFiles();

            MetadataFileReference[] references = BuildAssemblyReferences();

            return CSharpCompilation.Create(
                "Server",
                inputFiles.Select(file => CSharpSyntaxTree.ParseFile(Path.Combine(Path.GetDirectoryName(_projectPath), file))),
                references);

        }
    }
}