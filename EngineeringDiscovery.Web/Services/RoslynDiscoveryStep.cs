using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineeringDiscovery.Web.Services
{
    internal class RoslynDiscoveryStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Discovery;

        private readonly Investigation _investigation;
        public RoslynDiscoveryStep(Investigation investigation)
        {
            _investigation = investigation ?? throw new ArgumentNullException(nameof(investigation));
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            try
            {
                // Register MSBuild to ensure MSBuildWorkspace can load SDK-style projects
                try { if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults(); } catch { }

                var solutionPath = context.SolutionPath;
                if (string.IsNullOrWhiteSpace(solutionPath)) return;

                using var workspace = MSBuildWorkspace.Create();
                var solution = workspace.OpenSolutionAsync(solutionPath).GetAwaiter().GetResult();

                foreach (var proj in solution.Projects)
                {
                    try
                    {
                        var projectName = proj.Name ?? (proj.FilePath == null ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(proj.FilePath));
                        var compilation = proj.GetCompilationAsync().GetAwaiter().GetResult();
                        if (compilation == null) continue;

                        foreach (var tree in compilation.SyntaxTrees)
                        {
                            var model = compilation.GetSemanticModel(tree);
                            var root = tree.GetRoot();

                            // Find declared named types in this syntax tree
                            var declared = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
                            foreach (var decl in declared)
                            {
                                try
                                {
                                    var symbol = model.GetDeclaredSymbol(decl) as INamedTypeSymbol;
                                    if (symbol == null) continue;

                                    // Only consider types declared in source files belonging to the project
                                    if (symbol.Locations == null) continue;

                                    var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                                    var typeName = symbol.Name ?? string.Empty;

                                    var qn = !string.IsNullOrWhiteSpace(projectName)
                                        ? (!string.IsNullOrWhiteSpace(ns) ? $"{projectName}:{ns}.{typeName}" : $"{projectName}:{typeName}")
                                        : (!string.IsNullOrWhiteSpace(ns) ? $"{ns}.{typeName}" : $"{symbol.Locations.FirstOrDefault()?.SourceTree?.FilePath}:{typeName}");

                                    var typeObs = new TypeObservation
                                    {
                                        Project = projectName,
                                        Namespace = ns,
                                        TypeName = typeName,
                                        QualifiedName = qn,
                                        Kind = MapTypeKind(symbol.TypeKind),
                                        Accessibility = symbol.DeclaredAccessibility.ToString(),
                                        IsAbstract = symbol.IsAbstract,
                                        IsStatic = symbol.IsStatic,
                                        IsPartial = symbol.DeclaringSyntaxReferences.Length > 1,
                                        IsGeneric = symbol.IsGenericType,
                                        GenericParameterCount = symbol.TypeParameters.Length,
                                        BaseType = symbol.BaseType?.ToDisplayString(),
                                        ImplementedInterfaceCount = symbol.Interfaces.Length,
                                        MethodCount = symbol.GetMembers().OfType<IMethodSymbol>().Count(m => m.MethodKind == MethodKind.Ordinary),
                                        ConstructorCount = symbol.Constructors.Length,
                                        PropertyCount = symbol.GetMembers().OfType<IPropertySymbol>().Count(),
                                        FieldCount = symbol.GetMembers().OfType<IFieldSymbol>().Count(),
                                        EventCount = symbol.GetMembers().OfType<IEventSymbol>().Count()
                                    };

                                    try { context.TypeObservations.Add(typeObs); } catch { }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static EngineeringDiscovery.Core.Models.TypeKind MapTypeKind(Microsoft.CodeAnalysis.TypeKind k)
        {
            return k switch
            {
                Microsoft.CodeAnalysis.TypeKind.Interface => EngineeringDiscovery.Core.Models.TypeKind.Interface,
                Microsoft.CodeAnalysis.TypeKind.Struct => EngineeringDiscovery.Core.Models.TypeKind.Struct,
                Microsoft.CodeAnalysis.TypeKind.Enum => EngineeringDiscovery.Core.Models.TypeKind.Enum,
                Microsoft.CodeAnalysis.TypeKind.Delegate => EngineeringDiscovery.Core.Models.TypeKind.Delegate,
                _ => EngineeringDiscovery.Core.Models.TypeKind.Class,
            };
        }
    }
}
