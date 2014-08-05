using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using HttpClientGenerator.ClientGenerationModel;
using HttpClientGenerator.SemanticAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RestSharp;

namespace HttpClientGenerator
{
    internal class EndpointCollector
    {
        private readonly List<IMethodSymbol> _actionMethods;
        private readonly Func<SyntaxTree, SemanticModel> _semanticModelFactory;

        public EndpointCollector(List<IMethodSymbol> actionMethods, Func<SyntaxTree, SemanticModel> semanticModelFactory)
        {
            _actionMethods = actionMethods;
            _semanticModelFactory = semanticModelFactory;
        }

        public IEnumerable<ClientInfo> CollectEndpointInformation()
        {
            return ProduceClientInfos(_actionMethods.GroupBy(m => m.ContainingType));
        }

        public IEnumerable<SimpleType> CollectResponseTypes()
        {
            return _actionMethods.Where(m => !m.ReturnsVoid)
                                 .Select(m => m.ReturnType)
                                 .Select(CreateSimpleType);
        } 

        public IEnumerable<SimpleType> CollectParameterSimpleTypes()
        {
            return _actionMethods.SelectMany(p => p.Parameters).Where(IsComplexType).Select(p => p.Type).Distinct().Select(CreateSimpleType);
        }

        private SimpleType CreateSimpleType(ITypeSymbol ty)
        {
            if (ty is INamedTypeSymbol)
            {
                var nty = ty as INamedTypeSymbol;
                if (nty.IsGenericType)
                {
                    ty = nty.TypeArguments[0];
                }
            }
            return new SimpleType
            {
                Name = ty.Name,
                Members = ty.GetMembers()
                            .OfType<IPropertySymbol>()
                            .Select(m => new SimpleTypeMember
                            {
                                Name = m.Name,
                                TypeName = TypeHelper.GetTypeName(m.Type)
                            })
            };
        }

        private bool IsComplexType(IParameterSymbol arg)
        {
            return !arg.Type.IsValueType;
        }

        private IEnumerable<ClientInfo> ProduceClientInfos(IEnumerable<IGrouping<INamedTypeSymbol, IMethodSymbol>> typeActions)
        {
            return typeActions.Select(typeAction => new ClientInfo
            {
                ClientName = typeAction.Key.Name,
                RestEndpoints = CollectEndpoints(typeAction)
            });
        }

        private static Method GetRestMethod(IMethodSymbol action)
        {
            var attributeNames = action.GetAttributes()
                                       .Select(a => a.AttributeClass.Name);

            foreach (var name in attributeNames)
            {
                if (name == typeof(HttpGetAttribute).Name)
                {
                    return Method.GET;
                }

                if (name == typeof(HttpPostAttribute).Name)
                {
                    return Method.POST;
                }

                if (name == typeof(HttpPutAttribute).Name)
                {
                    return Method.PUT;
                }

                if (name == typeof(HttpDeleteAttribute).Name)
                {
                    return Method.DELETE;
                }
            }

            return Method.GET;
        }

        private static string CreateActionUri(IMethodSymbol action)
        {
            var classRoute = action.ContainingType.GetAttributes()
                                   .SingleOrDefault(a => a.AttributeClass.Name == typeof(RoutePrefixAttribute).Name);
            var actionRoute = action.GetAttributes()
                                    .Single(a => a.AttributeClass.Name == typeof(RouteAttribute).Name);

            string classRouteUrl = classRoute != null ? (string)classRoute.ConstructorArguments[0].Value : string.Empty;
            string actionRouteUrl = actionRoute.ConstructorArguments.Length > 0 ? (string)actionRoute.ConstructorArguments[0].Value : string.Empty;

            string routeUrl = classRouteUrl + actionRouteUrl;

            return routeUrl;
        }

        private IEnumerable<RestEndpointInfo> CollectEndpoints(IEnumerable<IMethodSymbol> typeAction)
        {
            return typeAction.Select(methodSymbol => new RestEndpointInfo
            {
                Name = methodSymbol.Name,
                Method = GetRestMethod(methodSymbol),
                Uri = CreateActionUri(methodSymbol),
                Parameters = GetParameters(methodSymbol),
                ReturnType = GetReturnTypeName(methodSymbol)
            });
        }

        private string GetReturnTypeName(IMethodSymbol methodSymbol)
        {
            if (methodSymbol.ReturnType.IsValueType)
            {
                return methodSymbol.ReturnType.Name;
            }

            if (methodSymbol.ReturnType.Name == typeof(IHttpActionResult).Name || 
                (methodSymbol.ReturnType is INamedTypeSymbol &&
                 ((methodSymbol.ReturnType as INamedTypeSymbol).IsGenericType) &&
                 ((INamedTypeSymbol)methodSymbol.ReturnType).TypeArguments[0].Name == typeof(IHttpActionResult).Name))
            {
                return FindLastReturnType(methodSymbol);
            }

            return TypeHelper.GetTypeName(methodSymbol.ReturnType);
        }

        private string FindLastReturnType(IMethodSymbol methodSymbol)
        {
            var storage = new ReturnStatementGenericTypes();

            var reference = methodSymbol.DeclaringSyntaxReferences[0];

            storage.Visit(reference.GetSyntax());

            InvocationExpressionSyntax call = storage.ReturnCalls.Last();

            if (call.ArgumentList.Arguments.Count > 0)
            {
                var t = _semanticModelFactory(reference.SyntaxTree);
                var type = t.GetTypeInfo(call.ArgumentList.Arguments[0].Expression);
                if (!(type.Type is IErrorTypeSymbol))
                {
                    return TypeHelper.GetTypeName(type.Type);
                }
                else
                {

                    var d = t.GetMethodBodyDiagnostics(reference.GetSyntax().Span);
                    var x = reference.GetSyntax();
                    string s = x.ToFullString();
                    Console.WriteLine(s);
                }
            }

            return "void";
        }

        private IEnumerable<EndpointParameter> GetParameters(IMethodSymbol methodSymbol)
        {
            return methodSymbol.Parameters.Select(
                p => new EndpointParameter
                {
                    Name = p.Name,
                    TypeName = TypeHelper.GetTypeName(p.Type)
                });
        }
    }
}