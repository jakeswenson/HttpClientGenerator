using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Microsoft.CodeAnalysis;
using RestSharp;

namespace HttpClientGenerator
{
    internal class EndpointCollector
    {
        private readonly List<IMethodSymbol> _actionMethods;
        private readonly INamedTypeSymbol _routeAttributeTypeSymbol;

        public EndpointCollector(List<IMethodSymbol> actionMethods, INamedTypeSymbol routeAttributeTypeSymbol)
        {
            _actionMethods = actionMethods;
            _routeAttributeTypeSymbol = routeAttributeTypeSymbol;
        }

        public IEnumerable<ClientInfo> CollectEndpointInformation()
        {
            return ProduceClientInfos(_actionMethods.GroupBy(m => m.ContainingType));
        }

        private IEnumerable<ClientInfo> ProduceClientInfos(IEnumerable<IGrouping<INamedTypeSymbol, IMethodSymbol>> typeActions)
        {
            foreach (var typeAction in typeActions)
            {
                yield return new ClientInfo
                {
                    ClientName = typeAction.Key.Name,
                    RestEndpoints = CollectEndpoints(typeAction)
                };
            }
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

        private IEnumerable<RestEndpointInfo> CollectEndpoints(IGrouping<INamedTypeSymbol, IMethodSymbol> typeAction)
        {
            foreach (var methodSymbol in typeAction)
            {
                yield return new RestEndpointInfo
                {
                    Name = methodSymbol.Name,
                    Method = GetRestMethod(methodSymbol),
                    Uri = CreateActionUri(methodSymbol)
                };
            }
        }
    }
}