using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal class CSharpRepositoryProvider : IRepositoryProvider
    {
        public bool CanLoad(string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot)) return false;
            // Heuristic: presence of .sln/.slnx/.csproj/.cs files
            var hasSln = Directory.GetFiles(repositoryRoot, "*.sln", SearchOption.AllDirectories).Any();
            var hasSlnx = Directory.GetFiles(repositoryRoot, "*.slnx", SearchOption.AllDirectories).Any();
            var hasCsproj = Directory.GetFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories).Any();
            var hasCs = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories).Any();
            return hasSln || hasSlnx || hasCsproj || hasCs;
        }

        public IReadOnlyList<CompilationContext> Load(string repositoryRoot)
        {
            var result = new List<CompilationContext>();

            try
            {
                try { if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults(); } catch { }

                using var workspace = MSBuildWorkspace.Create();

                // Strategy: try .sln, then .slnx, then csproj files, then loose cs files (handled as single-context)
                var sln = Directory.GetFiles(repositoryRoot, "*.sln", SearchOption.AllDirectories).FirstOrDefault();
                if (sln == null)
                {
                    sln = Directory.GetFiles(repositoryRoot, "*.slnx", SearchOption.AllDirectories).FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(sln) && File.Exists(sln))
                {
                    var solution = workspace.OpenSolutionAsync(sln).GetAwaiter().GetResult();
                    foreach (var proj in solution.Projects)
                    {
                        try
                        {
                            var compilation = proj.GetCompilationAsync().GetAwaiter().GetResult();
                            if (compilation == null) continue;
                            var ctx = CreateContextFromCompilation(proj.Name ?? string.Empty, proj.FilePath, compilation);
                            result.Add(ctx);
                        }
                        catch { }
                    }

                    if (result.Count > 0) return result;
                }

                // Try individual csproj files
                var csproj = Directory.GetFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(csproj) && File.Exists(csproj))
                {
                    var proj = workspace.OpenProjectAsync(csproj).GetAwaiter().GetResult();
                    var compilation = proj.GetCompilationAsync().GetAwaiter().GetResult();
                    if (compilation != null)
                    {
                        result.Add(CreateContextFromCompilation(proj.Name ?? string.Empty, proj.FilePath, compilation));
                        return result;
                    }
                }

                // Fallback: treat loose .cs files as a single context
                var csFiles = Directory.GetFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories);
                if (csFiles.Length > 0)
                {
                    // Create an ad-hoc project in workspace for these files
                    var adhoc = workspace.CurrentSolution.AddProject("AdhocProject", "AdhocProject.dll", LanguageNames.CSharp).WithCompilationOptions(new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                    var project = workspace.CurrentSolution.GetProject(adhoc.Id);
                    foreach (var f in csFiles)
                    {
                        try { project = project.AddDocument(Path.GetFileName(f), File.ReadAllText(f)).Project; } catch { }
                    }
                    var compilation = project.GetCompilationAsync().GetAwaiter().GetResult();
                    if (compilation != null)
                    {
                        result.Add(CreateContextFromCompilation("AdhocProject", null, compilation));
                        return result;
                    }
                }
            }
            catch { }

            return result;
        }

        private CompilationContext CreateContextFromCompilation(string projectName, string? projectFilePath, Compilation compilation)
        {
            var ctx = new CompilationContext { ProjectName = projectName ?? string.Empty, ProjectFilePath = projectFilePath };

            foreach (var tree in compilation.SyntaxTrees)
            {
                try
                {
                    var model = compilation.GetSemanticModel(tree);
                    var root = tree.GetRoot();
                    var declared = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
                    foreach (var decl in declared)
                    {
                        try
                        {
                            var symbol = model.GetDeclaredSymbol(decl) as INamedTypeSymbol;
                            if (symbol == null) continue;

                            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                            var typeName = symbol.Name ?? string.Empty;
                            var qn = !string.IsNullOrWhiteSpace(projectName)
                                ? (!string.IsNullOrWhiteSpace(ns) ? $"{projectName}:{ns}.{typeName}" : $"{projectName}:{typeName}")
                                : (!string.IsNullOrWhiteSpace(ns) ? $"{ns}.{typeName}" : $"{tree.FilePath}:{typeName}");

                            var td = new TypeDescriptor
                            {
                                Namespace = ns,
                                TypeName = typeName,
                                QualifiedName = qn,
                                Kind = symbol.TypeKind.ToString(),
                                Accessibility = symbol.DeclaredAccessibility.ToString(),
                                IsAbstract = symbol.IsAbstract,
                                IsStatic = symbol.IsStatic,
                                IsGeneric = symbol.IsGenericType,
                                GenericParameterCount = symbol.TypeParameters.Length,
                                BaseType = symbol.BaseType?.ToDisplayString(),
                                MethodCount = symbol.GetMembers().OfType<IMethodSymbol>().Count(m => m.MethodKind == MethodKind.Ordinary),
                                ConstructorCount = symbol.Constructors.Length,
                                PropertyCount = symbol.GetMembers().OfType<IPropertySymbol>().Count(),
                                FieldCount = symbol.GetMembers().OfType<IFieldSymbol>().Count(),
                                EventCount = symbol.GetMembers().OfType<IEventSymbol>().Count(),
                                SourceFilePath = tree.FilePath
                            };

                            ctx.Types.Add(td);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return ctx;
        }
    }
}
