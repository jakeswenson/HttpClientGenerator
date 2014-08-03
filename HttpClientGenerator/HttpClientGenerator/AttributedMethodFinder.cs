using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace HttpClientGenerator
{
    public class AttributedMethodFinder : SymbolVisitor<IEnumerable<IMethodSymbol>>
    {
        private readonly INamedTypeSymbol _routeAttirbute;

        public AttributedMethodFinder(INamedTypeSymbol routeAttirbute)
        {
            _routeAttirbute = routeAttirbute;
        }

        public override IEnumerable<IMethodSymbol> DefaultVisit(ISymbol symbol)
        {
            throw new Exception();
        }

        public override IEnumerable<IMethodSymbol> VisitMethod(IMethodSymbol symbol)
        {
            if (symbol.GetAttributes()
                      .Any(a => a.AttributeClass == _routeAttirbute))
            {
                yield return symbol;
            }
        }

        public override IEnumerable<IMethodSymbol> VisitNamedType(INamedTypeSymbol symbol)
        {
            return from member in symbol.GetMembers()
                   where member is IMethodSymbol
                   from result in VisitMethod((IMethodSymbol)member)
                   select result;
        }

        public List<IMethodSymbol> FindMethodsInTypes(IEnumerable<INamedTypeSymbol> namedTypeSymbols)
        {
            return namedTypeSymbols.SelectMany(ty => ty.Accept(this)).ToList();
        }
    }
}