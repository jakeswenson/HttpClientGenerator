using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RestSharp;

namespace HttpClientGenerator
{
    internal class ClientEmittor
    {
        private readonly IEnumerable<ClientInfo> _endpoints;
        private readonly List<IMethodSymbol> _actionMethods;
        private Lazy<SyntaxTree> tree = new Lazy<SyntaxTree>();
        HashSet<string> addedSet = new HashSet<string>();
        List<SyntaxTree> list = new List<SyntaxTree>();

        public ClientEmittor()
        {
        }

        private ClientEmittor(IEnumerable<ClientInfo> endpoints, List<IMethodSymbol> actionMethods)
        {
            _endpoints = endpoints;
            _actionMethods = actionMethods;
        }

        public void DumpTree()
        {
            var tree = GenerateClientSyntaxTree(
                clientCodeNamespace: "Service.Client",
                actions: _actionMethods);

            Console.WriteLine(tree);
        }

        public ClientEmittor WithEndpoints(IEnumerable<ClientInfo> endpoints, List<IMethodSymbol> actionMethods)
        {
            return new ClientEmittor(endpoints, actionMethods);
        }

        private SyntaxTree GenerateClientSyntaxTree(string clientCodeNamespace, IEnumerable<IMethodSymbol> actions)
        {
            var clients = from action in actions
                          group action by action.ContainingType
                          into typeMethods
                          select CreateClientWrapperClass(typeMethods);

            var clientNamespace = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(clientCodeNamespace),
                SyntaxFactory.List<ExternAliasDirectiveSyntax>(),
                SyntaxFactory.List<UsingDirectiveSyntax>(),
                SyntaxFactory.List<MemberDeclarationSyntax>(clients));

            var unit = SyntaxFactory.CompilationUnit(
                SyntaxFactory.List<ExternAliasDirectiveSyntax>(),
                SyntaxFactory.List(
                    new[]
                    {
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(RestClient).Namespace)), 
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(List<>).Namespace)),
                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(Guid).Namespace)),
                    }),
                SyntaxFactory.List<AttributeListSyntax>(),
                SyntaxFactory.List(new MemberDeclarationSyntax[] { clientNamespace }));

            var tree = CSharpSyntaxTree.Create(unit);

            return tree;
        }

        private ClassDeclarationSyntax CreateClientWrapperClass(IGrouping<INamedTypeSymbol, IMethodSymbol> typeMethods)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            return SyntaxFactory.ClassDeclaration(typeMethods.Key.Name)
                                .AddBaseListTypes(SyntaxFactory.ParseTypeName(typeof(RestClient).FullName))
                                .AddMembers(
                                    CreateActionMethods(typeMethods)
                                        .ToArray())
                                .WithLeadingTrivia(newlineTrivia)
                                .WithTrailingTrivia(newlineTrivia);
        }

        private IEnumerable<MemberDeclarationSyntax> CreateActionMethods(IEnumerable<IMethodSymbol> typeMethods)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            foreach (var methodSymbol in typeMethods)
            {
                yield return CreateClientInvocationMethod(methodSymbol);
            }

            yield return SyntaxFactory.ConstructorDeclaration(".ctor")
                                      .WithParameterList(SyntaxFactory.ParseParameterList("(string baseUri)"))
                                      .WithInitializer(
                                          SyntaxFactory.ConstructorInitializer(
                                              kind: SyntaxKind.BaseConstructorInitializer,
                                              argumentList: SyntaxFactory.ParseArgumentList("(baseUri)")))
                                      .WithBody(SyntaxFactory.Block())
                                      .WithLeadingTrivia(newlineTrivia, newlineTrivia);
        }

        private MemberDeclarationSyntax CreateClientInvocationMethod(IMethodSymbol action)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            var nullExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                                              .WithLeadingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " "));

            var trees = Task.WhenAll(action.Parameters.Where(p => p.Type.Name != "Guid")
                                           .SelectMany(p => p.Type.DeclaringSyntaxReferences.Select(async d => Tuple.Create(p.Type, await d.GetSyntaxAsync())))).Result;

            foreach (var syntaxTree in trees)
            {
                if (!addedSet.Contains(syntaxTree.Item1.Name))
                {
                    //list.Add(syntaxTree.Item2);
                }
            }

            return SyntaxFactory.MethodDeclaration(identifier: action.Name, returnType: SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task<IEnumerable<string>>"))
                                .AddParameterListParameters(
                                    action.Parameters.SelectMany(p => p.DeclaringSyntaxReferences.Select(d => Tuple.Create(p, d.GetSyntax())).Where(t => t.Item2 is ParameterSyntax).Select(t => Tuple.Create(t.Item1, (ParameterSyntax)t.Item2))).Select(
                                        p => SyntaxFactory.Parameter(p.Item2.Identifier).WithType(
                                            SyntaxFactory.ParseTypeName(
                                                p.Item1.Type.ToDisplayString()
                                                 ))).ToArray()
                )
                                .AddBodyStatements(
                                    SyntaxFactory.LocalDeclarationStatement(
                                        SyntaxFactory.VariableDeclaration(
                                            SyntaxFactory.ParseTypeName(typeof(RestRequest).FullName),
                                            SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>()
                                                         .Add(SyntaxFactory.VariableDeclarator("@_request")
                                                                           .WithInitializer(
                                                                               SyntaxFactory.EqualsValueClause(
                                                                                   SyntaxFactory.ObjectCreationExpression(
                                                                                       SyntaxFactory.ParseTypeName(typeof(RestRequest).FullName)).AddArgumentListArguments(
                                                                                           SyntaxFactory.Argument(CreateActionUri(action)),
                                                                                           SyntaxFactory.Argument(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ParseTypeName(typeof(Method).FullName), GetActionIdentifier(action)))
                                                                                       )))))),
                                    SyntaxFactory.ExpressionStatement(
                                        SyntaxFactory.InvocationExpression(
                                            SyntaxFactory.GenericName("Execute").AddTypeArgumentListArguments(SyntaxFactory.ParseTypeName("object")))
                                                     .AddArgumentListArguments(
                                                         SyntaxFactory.Argument(SyntaxFactory.IdentifierName("@_request"))
                                            ))
                                                 .WithTrailingTrivia(newlineTrivia)
                                                 .WithLeadingTrivia(newlineTrivia),
                                    SyntaxFactory.ReturnStatement(nullExpression)
                                                 .WithTrailingTrivia(newlineTrivia))
                                .WithTrailingTrivia(newlineTrivia).WithLeadingTrivia(newlineTrivia);
        }

        private static IdentifierNameSyntax GetActionIdentifier(IMethodSymbol action)
        {
            var s = action.GetAttributes()
                          .Select(a => a.AttributeClass.Name);
            foreach (var name in s)
            {
                if (name == typeof(HttpGetAttribute).Name)
                {
                    return SyntaxFactory.IdentifierName("GET");
                }

                if (name == typeof(HttpPostAttribute).Name)
                {
                    return SyntaxFactory.IdentifierName("POST");
                }

                if (name == typeof(HttpPutAttribute).Name)
                {
                    return SyntaxFactory.IdentifierName("PUT");
                }

                if (name == typeof(HttpDeleteAttribute).Name)
                {
                    return SyntaxFactory.IdentifierName("DELETE");
                }
            }

            return SyntaxFactory.IdentifierName("GET");
        }

        private static LiteralExpressionSyntax CreateActionUri(IMethodSymbol action)
        {
            var classRoute = action.ContainingType.GetAttributes()
                                   .SingleOrDefault(a => a.AttributeClass.Name == typeof(RoutePrefixAttribute).Name);
            var actionRoute = action.GetAttributes()
                                    .Single(a => a.AttributeClass.Name == typeof(RouteAttribute).Name);

            string classRouteUrl = classRoute != null ? (string)classRoute.ConstructorArguments[0].Value : string.Empty;
            string actionRouteUrl = actionRoute.ConstructorArguments.Length > 0 ? (string)actionRoute.ConstructorArguments[0].Value : string.Empty;

            string routeUrl = classRouteUrl + actionRouteUrl;

            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(routeUrl));
        }

        public CSharpCompilation CreateCompilation(ReferenceCache referenceCache)
        {
            return CSharpCompilation.Create(
                "Clients",
                syntaxTrees: new[] { tree.Value }.Concat(list),
                references:
                    new[]
                    {
                        referenceCache.MSCoreLib,
                        referenceCache.SystemRuntime,
                        referenceCache.AssemblyReferenceForType<HttpClient>(),
                        referenceCache.AssemblyReferenceForType<RestClient>(),
                        referenceCache.AssemblyReferenceForType<Uri>(),
                        referenceCache.ForAssemblyLocation(@"C:\pf\Stash\aco\Composite\Core\bin\Debug\PF.Aco.Entities.dll")
                    },
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}