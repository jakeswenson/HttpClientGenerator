using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Web.Http;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HttpClientGenerator
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args == null ||
                args.Length < 1)
            {
                Console.WriteLine("Please provide a project (csproj path) containing ApiControllers");
                return;
            }

            string projectPath = args[0];
            var doc = XDocument.Load(projectPath);
            var ns = XNamespace.Get("http://schemas.microsoft.com/developer/msbuild/2003");
            var x = doc.Descendants(ns + "Compile");
            var inputFiles = x.Select(
                n => n.Attribute("Include")
                      .Value);

            string path = RuntimeEnvironment.GetRuntimeDirectory();
            var resolver = new MetadataFileReferenceResolver(ImmutableArray.Create(path), baseDirectory: null);

            var systemRuntimeReference = resolver.ResolveReference("System.Runtime.dll", baseFilePath: null);

            var references =
                new[]
                {
                    typeof(object).Assembly.Location, 
                    typeof(ApiController).Assembly.Location, 
                    systemRuntimeReference
                }.Select(reference => new MetadataFileReference(reference))
                 .ToArray();

            Console.WriteLine("Files: {0}", string.Join(", ", inputFiles));

            var serviceCompilation = CSharpCompilation.Create(
                "Server",
                inputFiles.Select(file => CSharpSyntaxTree.ParseFile(Path.Combine(Path.GetDirectoryName(projectPath), file))),
                references);

            var apiController = serviceCompilation.GetTypeByMetadataName("System.Web.Http.ApiController");
            var routeAttirbute = serviceCompilation.GetTypeByMetadataName("System.Web.Http.RouteAttribute");

            var types =
                serviceCompilation
                    .Assembly
                    .Accept(new ControllerFinder(apiController))
                    .ToList();

            var actions = types.SelectMany(ty => ty.Accept(new ActionFinder(routeAttirbute))).ToList();

            Console.WriteLine("Found: {0}", string.Join(", ", types.Select(ty => ty.Name)));
            Console.WriteLine("Actions: {0}", string.Join(", ", actions.Select(act => act.ContainingType.Name + "." + act.Name)));

            var clients = from action in actions
                          group action by action.ContainingType
                              into typeMethods
                              select CreateClientWrapperClass(typeMethods, routeAttirbute);

            var clientNamespace = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName("WebService.Clients"),
                SyntaxFactory.List<ExternAliasDirectiveSyntax>(),
                SyntaxFactory.List<UsingDirectiveSyntax>(),
                SyntaxFactory.List<MemberDeclarationSyntax>(clients));

            var unit = SyntaxFactory.CompilationUnit(
                SyntaxFactory.List<ExternAliasDirectiveSyntax>(),
                SyntaxFactory.List(new[] { SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")) }),
                SyntaxFactory.List<AttributeListSyntax>(),
                SyntaxFactory.List(new MemberDeclarationSyntax[] { clientNamespace }));
            var tree = CSharpSyntaxTree.Create(unit);

            Console.WriteLine(tree);

            var compilation = CSharpCompilation.Create(
                "Clients",
                syntaxTrees: new[] { tree },
                references:
                    new[]
                    {
                        references[0], 
                        references[2], 
                        new MetadataFileReference(typeof(HttpClient).Assembly.Location), 
                        new MetadataFileReference(typeof(Uri).Assembly.Location)
                    },
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var d = compilation.GetDiagnostics();

            foreach (var diagnostic in d)
            {
                Console.WriteLine("{0} {1}", diagnostic.Severity, diagnostic.GetMessage());
            }

            Console.ReadKey(true);
        }

        private static ClassDeclarationSyntax CreateClientWrapperClass(IGrouping<INamedTypeSymbol, IMethodSymbol> typeMethods, INamedTypeSymbol routeAttirbute)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            return SyntaxFactory.ClassDeclaration(typeMethods.Key.Name)
                                .AddBaseListTypes(SyntaxFactory.ParseTypeName("System.Net.Http.HttpClient"))
                                .AddMembers(
                                    CreateActionMethods(typeMethods, routeAttirbute)
                                        .ToArray()).WithLeadingTrivia(newlineTrivia).WithTrailingTrivia(newlineTrivia);
        }

        private static IEnumerable<MemberDeclarationSyntax> CreateActionMethods(IEnumerable<IMethodSymbol> typeMethods, INamedTypeSymbol routeAttirbute)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            foreach (var methodSymbol in typeMethods)
            {
                yield return CreateClientInvocationMethod(methodSymbol, routeAttirbute);
            }

            yield return SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("System.Uri"),
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.VariableDeclarator("_baseUri")
                }))).WithLeadingTrivia(newlineTrivia);

            yield return SyntaxFactory.ConstructorDeclaration(".ctor")
                                      .WithParameterList(SyntaxFactory.ParseParameterList("(System.Uri uri)"))
            .AddBodyStatements(SyntaxFactory.ExpressionStatement(SyntaxFactory.ParseExpression("_baseUri = uri")))
            .WithLeadingTrivia(newlineTrivia, newlineTrivia);
        }

        private static MemberDeclarationSyntax CreateClientInvocationMethod(IMethodSymbol action, INamedTypeSymbol routeAttirbute)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            var nullExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression).WithLeadingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " "));

            return SyntaxFactory.MethodDeclaration(identifier: action.Name, returnType: SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task<IEnumerable<string>>"))
                                .AddBodyStatements(
                                        SyntaxFactory.ExpressionStatement(
                                            SyntaxFactory.InvocationExpression(
                                                SyntaxFactory.ParseName("PostAsync"),
                                                SyntaxFactory.ArgumentList(
                                                    SyntaxFactory.SeparatedList(
                                                        new[]
                                                        {
                                                            SyntaxFactory.Argument(CreateActionUri(action, routeAttirbute)), 
                                                            SyntaxFactory.Argument(nullExpression)
                                                        })))).WithTrailingTrivia(newlineTrivia).WithLeadingTrivia(newlineTrivia),
                                        SyntaxFactory.ReturnStatement(nullExpression).WithTrailingTrivia(newlineTrivia))
                                        .WithTrailingTrivia(newlineTrivia);
        }

        private static LiteralExpressionSyntax CreateActionUri(IMethodSymbol action, INamedTypeSymbol routeAttirbute)
        {
            var classRoute = action.ContainingType.GetAttributes()
                                   .SingleOrDefault(a => a.AttributeClass == routeAttirbute);
            var actionRoute = action.GetAttributes()
                                    .Single(a => a.AttributeClass == routeAttirbute);

            string classRouteUrl = (string)classRoute.ConstructorArguments[0].Value;
            string actionRouteUrl = actionRoute.ConstructorArguments.Length > 0 ? (string)actionRoute.ConstructorArguments[0].Value : string.Empty;

            string routeUrl = classRouteUrl + actionRouteUrl;

            return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal('"' + routeUrl + '"', routeUrl));
        }
    }

    public class ActionFinder : SymbolVisitor<IEnumerable<IMethodSymbol>>
    {
        private readonly INamedTypeSymbol _routeAttirbute;

        public ActionFinder(INamedTypeSymbol routeAttirbute)
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
    }

    public class ControllerFinder : SymbolVisitor<IEnumerable<INamedTypeSymbol>>
    {
        private readonly INamedTypeSymbol _apiController;

        public ControllerFinder(INamedTypeSymbol apiController)
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
    }
}