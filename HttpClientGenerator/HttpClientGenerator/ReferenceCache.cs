using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;

namespace HttpClientGenerator
{
    internal class ReferenceCache
    {
        private readonly ConcurrentDictionary<string, MetadataFileReference> _metadataFileReferences = new ConcurrentDictionary<string, MetadataFileReference>();

        private readonly Lazy<string> _resolvedSystemRuntimeLocation = new Lazy<string>(ResolveSystemRuntimeLocation);

        private static string ResolveSystemRuntimeLocation()
        {
            string runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
            var resolver = new MetadataFileReferenceResolver(searchPaths: ImmutableArray.Create(runtimeDirectory), baseDirectory: null);
            return resolver.ResolveReference("System.Runtime.dll", baseFilePath: null);
        }

        public MetadataFileReference AssemblyReferenceForType<T>()
        {
            string assemblyLocation = typeof(T).Assembly.Location;
            return ForAssemblyLocation(assemblyLocation);
        }

        public MetadataFileReference ForAssemblyLocation(string assemblyLocation)
        {
            return _metadataFileReferences.GetOrAdd(assemblyLocation, location => new MetadataFileReference(fullPath: location, kind: MetadataImageKind.Assembly));
        }

        public MetadataFileReference SystemRuntime
        {
            get
            {
                return ForAssemblyLocation(_resolvedSystemRuntimeLocation.Value);
            }
        }

        public MetadataFileReference MSCoreLib
        {
            get
            {
                return AssemblyReferenceForType<object>();
            }
        }
    }
}