using System.Linq;
using Microsoft.CodeAnalysis;

namespace HttpClientGenerator
{
    public static class TypeHelper
    {
        public static string GetTypeName(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol && ((INamedTypeSymbol)type).IsGenericType)
            {
                return string.Concat(type.Name, '<', string.Join(", ", ((INamedTypeSymbol)type).TypeArguments.Select(GetTypeName)), '>');
            }

            return type.Name;
        }
    }
}