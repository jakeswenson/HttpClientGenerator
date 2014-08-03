using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace HttpClientGenerator
{
    public class SubTypeFinder : SymbolVisitor<IEnumerable<INamedTypeSymbol>>
    {
        private readonly INamedTypeSymbol _apiController;

        public SubTypeFinder(INamedTypeSymbol apiController)
        {
            _apiController = apiController;
        }

        public override IEnumerable<INamedTypeSymbol> DefaultVisit(ISymbol symbol)
        {
            return Enumerable.Empty<INamedTypeSymbol>();
        }

        public override IEnumerable<INamedTypeSymbol> VisitAssembly(IAssemblySymbol symbol)
        {
            return VisitNamespace(symbol.GlobalNamespace);
        }

        public override IEnumerable<INamedTypeSymbol> VisitNamespace(INamespaceSymbol symbol)
        {
            return from namespaceOrTypeSymbol in symbol.GetMembers()
                   from type in namespaceOrTypeSymbol.Accept(this)
                   select type;
        }

        public override IEnumerable<INamedTypeSymbol> VisitNamedType(INamedTypeSymbol symbol)
        {
            Console.WriteLine("Type: {0}", symbol.Name);
            if (symbol.BaseType != _apiController)
            {
                yield break;
            }

            yield return symbol;
        }

        public INamedTypeSymbol[] FindTypes(IAssemblySymbol assembly)
        {
            return assembly.Accept(this).ToArray();
        }
    }
}