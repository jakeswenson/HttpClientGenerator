﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Http;
using HttpClientGenerator.Arguments;
using HttpClientGenerator.ClientGenerationModel;
using HttpClientGenerator.Projects;
using HttpClientGenerator.References;
using HttpClientGenerator.SemanticAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PowerArgs;

namespace HttpClientGenerator
{
    internal class Program
    {
        private readonly Options _options;

        private Program(Options options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _options = options;
        }

        private static void Main(string[] args)
        {
            try
            {
                var parsed = Args.Parse<Options>(args);
                var program = new Program(parsed);
                program.BuildClient();
            }
            catch (ArgException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ArgUsage.GetUsage<Options>());
            }
        }

        private void BuildClient()
        {
            var referenceCache = new ReferenceCache();

            var projectCompilationFactory = new ProjectCompilationFactory(_options.ProjectPath, referenceCache);

            CSharpCompilation serviceCompilation = projectCompilationFactory.CreateCompilation();

            INamedTypeSymbol apiControllerTypeSymbol = serviceCompilation.GetTypeByMetadataName(typeof(ApiController).FullName);
            INamedTypeSymbol routeAttributeTypeSymbol = serviceCompilation.GetTypeByMetadataName(typeof(RouteAttribute).FullName);

            var typeFinder = new SubTypeFinder(apiControllerTypeSymbol);

            INamedTypeSymbol[] controllerTypes = typeFinder.FindTypes(serviceCompilation.Assembly);

            var attributedMethodFinder = new AttributedMethodFinder(routeAttributeTypeSymbol);

            List<IMethodSymbol> actionMethods = attributedMethodFinder.FindMethodsInTypes(controllerTypes);

            Console.WriteLine("Found: {0}", string.Join(", ", controllerTypes.Select(ty => ty.Name)));
            Console.WriteLine("Actions: {0}", string.Join(", ", actionMethods.Select(act => act.ContainingType.Name + "." + act.Name)));
            
            var actionCollector = new EndpointCollector(actionMethods, serviceCompilation.GetSemanticModel);
            IEnumerable<ClientInfo> endpoints = actionCollector.CollectEndpointInformation();

            var clientGenerator = ClientEmitter.WithEndpoints(endpoints, actionCollector.CollectParameterSimpleTypes());
            
            clientGenerator.DumpTree();

            var compilation = clientGenerator.CreateCompilation(referenceCache);

            PrintDiagNostics(compilation);

            if (Debugger.IsAttached)
            {
                Console.ReadKey(true);
            }
        }

        private static void PrintDiagNostics(CSharpCompilation compilation)
        {
            var d = compilation.GetDiagnostics();

            foreach (var diagnostic in d)
            {
                Console.WriteLine("{0} {1} {2}", diagnostic.Severity, diagnostic.GetMessage(), diagnostic.Location);
            }
        }
    }
}