using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using HttpClientGenerator.ClientGenerationModel;
using HttpClientGenerator.References;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RestSharp;

namespace HttpClientGenerator
{
    internal class ClientEmitter
    {
        private readonly IEnumerable<ClientInfo> _endpoints;
        private readonly IEnumerable<SimpleType> _collectParameterSimpleTypes;
        private Lazy<SyntaxTree> tree;

        private ClientEmitter(IEnumerable<ClientInfo> endpoints, IEnumerable<SimpleType> collectParameterSimpleTypes)
        {
            _endpoints = endpoints;
            _collectParameterSimpleTypes = collectParameterSimpleTypes;
            tree = new Lazy<SyntaxTree>(() => CreateSyntaxTree(_endpoints, _collectParameterSimpleTypes));
        }

        private static SyntaxTree CreateSyntaxTree(IEnumerable<ClientInfo> endpoints, IEnumerable<SimpleType> collectedSimpleTypes)
        {
            var types = endpoints.Select(EmitClass).ToArray<MemberDeclarationSyntax>();

            var clientNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("Clients"))
                    .AddMembers(types)
                    .AddMembers(CreateSimpleTypes(collectedSimpleTypes).ToArray<MemberDeclarationSyntax>());

            var unit = SyntaxFactory.CompilationUnit()
                                    .AddUsings(
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(RestClient).Namespace)),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(IEnumerable<>).Namespace)),
                                        SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(typeof(Guid).Namespace)))
                                    .AddMembers(clientNamespace);

            return SyntaxFactory.SyntaxTree(unit);
        }

        private static IEnumerable<ClassDeclarationSyntax> CreateSimpleTypes(IEnumerable<SimpleType> collectedSimpleTypes)
        {
            return collectedSimpleTypes.Select(EmitSimpleClass);
        }

        private static ClassDeclarationSyntax EmitSimpleClass(SimpleType ty)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            return SyntaxFactory.ClassDeclaration(ty.Name)
                                .AddMembers(EmitTypeProperties(ty).ToArray<MemberDeclarationSyntax>())
                                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                                .WithLeadingTrivia(newlineTrivia)
                                .WithTrailingTrivia(newlineTrivia);
        }

        private static IEnumerable<PropertyDeclarationSyntax> EmitTypeProperties(SimpleType ty)
        {
            return ty.Members.Select(EmitSimpleTypeProperty);
        }

        private static PropertyDeclarationSyntax EmitSimpleTypeProperty(SimpleTypeMember mem)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(mem.TypeName), mem.Name)
                                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                                .AddAccessorListAccessors(
                                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration),
                                    SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration))
                                .WithLeadingTrivia(newlineTrivia)
                                .WithTrailingTrivia(newlineTrivia);
        }

        private static ClassDeclarationSyntax EmitClass(ClientInfo clientInfo)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            return SyntaxFactory.ClassDeclaration(clientInfo.ClientName)
                                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                                .AddBaseListTypes(SyntaxFactory.ParseTypeName(typeof(RestClient).FullName))
                                .AddMembers(EmitMembers(clientInfo.RestEndpoints))
                                .AddMembers(EmitConstructor())
                                .WithLeadingTrivia(newlineTrivia)
                                .WithTrailingTrivia(newlineTrivia);
        }

        private static MemberDeclarationSyntax EmitConstructor()
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            return SyntaxFactory.ConstructorDeclaration(".ctor")
                                .WithParameterList(SyntaxFactory.ParseParameterList("(string baseUri)"))
                                .WithInitializer(
                                    SyntaxFactory.ConstructorInitializer(
                                        kind: SyntaxKind.BaseConstructorInitializer,
                                        argumentList: SyntaxFactory.ParseArgumentList("(baseUri)")))
                                .WithBody(SyntaxFactory.Block())
                                .WithLeadingTrivia(newlineTrivia, newlineTrivia)
                                .WithTrailingTrivia(newlineTrivia);
        }

        private static MemberDeclarationSyntax[] EmitMembers(IEnumerable<RestEndpointInfo> restEndpoints)
        {
            return restEndpoints.Select(GenerateMethod).ToArray<MemberDeclarationSyntax>();
        }

        private static MethodDeclarationSyntax GenerateMethod(RestEndpointInfo restEndpointInfo)
        {
            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");

            TypeSyntax returnType;

            if (restEndpointInfo.ReturnType != "void")
            {
                returnType = SyntaxFactory.GenericName("System.Threading.Tasks.Task")
                                          .WithTypeArgumentList(
                                              SyntaxFactory.TypeArgumentList()
                                                           .AddArguments(SyntaxFactory.ParseTypeName(restEndpointInfo.ReturnType)));
            }
            else
            {
                returnType = SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task");
            }

            return SyntaxFactory.MethodDeclaration(returnType, restEndpointInfo.Name)
                                .AddParameterListParameters(restEndpointInfo.Parameters.Select(GenerateParameter).ToArray())
                                .AddBodyStatements(GenerateMethodBody(restEndpointInfo))
                                .WithTrailingTrivia(newlineTrivia).WithLeadingTrivia(newlineTrivia);
        }

        private static StatementSyntax[] GenerateMethodBody(RestEndpointInfo restEndpointInfo)
        {

            var newlineTrivia = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, "\r\n");
            var nullExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                                              .WithLeadingTrivia(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " "));
            return new StatementSyntax[]
            {
                SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(
                        SyntaxFactory.ParseTypeName(typeof(RestRequest).FullName),
                        SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>()
                                     .Add(
                                         SyntaxFactory.VariableDeclarator("@_request")
                                                      .WithInitializer(
                                                          SyntaxFactory.EqualsValueClause(
                                                              SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(typeof(RestRequest).FullName))
                                                                           .AddArgumentListArguments(
                                                                               SyntaxFactory.Argument(
                                                                               SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(restEndpointInfo.Uri))),
                                                                               SyntaxFactory.Argument(
                                                                                   SyntaxFactory.MemberAccessExpression(
                                                                                       SyntaxKind.SimpleMemberAccessExpression,
                                                                                       SyntaxFactory.ParseTypeName(typeof(Method).FullName),
                                                                                       SyntaxFactory.IdentifierName(restEndpointInfo.Method.ToString()))))))))),
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.GenericName("Execute")
                                     .AddTypeArgumentListArguments(SyntaxFactory.ParseTypeName("object")))
                                 .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("@_request"))))
                             .WithTrailingTrivia(newlineTrivia)
                             .WithLeadingTrivia(newlineTrivia),
                SyntaxFactory.ReturnStatement(nullExpression)
                             .WithTrailingTrivia(newlineTrivia)
            };
        }

        private static ParameterSyntax GenerateParameter(EndpointParameter endpointParameter)
        {
            return SyntaxFactory.Parameter(SyntaxFactory.Identifier(endpointParameter.Name))
                                .WithType(SyntaxFactory.ParseTypeName(endpointParameter.TypeName));
        }

        public void DumpTree()
        {
            Console.WriteLine(tree.Value);
        }

        public static ClientEmitter WithEndpoints(IEnumerable<ClientInfo> endpoints, IEnumerable<SimpleType> collectParameterSimpleTypes)
        {
            return new ClientEmitter(endpoints, collectParameterSimpleTypes);
        }

        public CSharpCompilation CreateCompilation(ReferenceCache referenceCache)
        {
            return CSharpCompilation.Create(
                "Clients",
                syntaxTrees: new[] { tree.Value },
                references:
                    new[]
                    {
                        referenceCache.MSCoreLib,
                        referenceCache.SystemRuntime,
                        referenceCache.AssemblyReferenceForType<HttpClient>(),
                        referenceCache.AssemblyReferenceForType<RestClient>(),
                        referenceCache.AssemblyReferenceForType<Uri>(),
                        //referenceCache.ForAssemblyLocation(@"C:\pf\Stash\aco\Composite\Core\bin\Debug\PF.Aco.Entities.dll")
                    },
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}