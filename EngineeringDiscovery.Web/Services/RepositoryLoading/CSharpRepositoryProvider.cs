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

            void Log(string repoRoot, string msg)
            {
                try
                {
                    var dir = System.IO.Path.Combine(repoRoot ?? string.Empty, ".ed-logs");
                    System.IO.Directory.CreateDirectory(dir);
                    var file = System.IO.Path.Combine(dir, "provider-load.log");
                    var line = $"[{DateTime.UtcNow:O}] {msg}\n";
                    System.IO.File.AppendAllText(file, line);
                }
                catch { }
            }

            try
            {
                try
                {
                    if (!MSBuildLocator.IsRegistered)
                    {
                        // If any Microsoft.Build assemblies are already loaded into the AppDomain
                        // RegisterDefaults will throw InvalidOperationException. Detect loaded
                        // assemblies and skip registration when that is the case to avoid the
                        // runtime error while preserving registration when possible.
                        var anyMsbuildLoaded = AppDomain.CurrentDomain.GetAssemblies()
                            .Any(a => {
                                try { return a.GetName().Name.StartsWith("Microsoft.Build", StringComparison.OrdinalIgnoreCase); } catch { return false; }
                            });

                        if (!anyMsbuildLoaded)
                        {
                            try { MSBuildLocator.RegisterDefaults(); } catch (InvalidOperationException) { /* already loaded; continue */ }
                        }
                        else
                        {
                        }
                    }
                }
                catch { }

                using var workspace = MSBuildWorkspace.Create();
                // Capture workspace diagnostics so provider can continue when some projects fail to load.
                try
                {
                    workspace.WorkspaceFailed += (s, e) =>
                    {
                        try { Log(repositoryRoot, $"MSBuild WorkspaceFailed: {e.Diagnostic.Message}"); } catch { }
                    };
                }
                catch { }

                // Repository-first loading: discover all csproj files beneath repositoryRoot and open each project independently.
                // A solution file is optional and not required for repository ingestion.
                try
                {
                    var csprojFiles = Directory.GetFiles(repositoryRoot ?? string.Empty, "*.csproj", SearchOption.AllDirectories);
                    if (csprojFiles != null && csprojFiles.Length > 0)
                    {
                        foreach (var csprojPath in csprojFiles)
                        {
                            try
                            {
                                // Avoid opening the same project twice — MSBuildWorkspace will include opened projects in CurrentSolution.
                                var existing = workspace.CurrentSolution.Projects.FirstOrDefault(p =>
                                    string.Equals(p.FilePath, csprojPath, StringComparison.OrdinalIgnoreCase));

                                var proj = existing ?? workspace.OpenProjectAsync(csprojPath).GetAwaiter().GetResult();
                                var compilation = proj.GetCompilationAsync().GetAwaiter().GetResult();
                                if (compilation == null) continue;
                                var ctx = CreateContextFromCompilation(proj.Name ?? string.Empty, proj.FilePath, compilation);
                                result.Add(ctx);
                            }
                            catch (Microsoft.Build.Exceptions.InvalidProjectFileException ex)
                            {
                                try { Log(repositoryRoot, $"Invalid project file '{csprojPath}': {ex.Message}"); } catch { }
                            }
                            catch (ArgumentException aex)
                            {
                                // MSBuildWorkspace may throw when a project is already part of the workspace; skip duplicates.
                                try { Log(repositoryRoot, $"Skipped duplicate project '{csprojPath}': {aex.Message}"); } catch { }
                                continue;
                            }
                            catch (Exception ex)
                            {
                                try { Log(repositoryRoot, $"Failed to load project '{csprojPath}': {ex.Message}"); } catch { }
                            }
                        }

                        if (result.Count > 0) return result;
                    }
                }
                catch { }

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

        // Removed string-based mappings. Discovery expects canonical engineering enums.

        private static EngineeringTypeKind MapEngineeringTypeKind(TypeKind typeKind)
        {
            return typeKind switch
            {
                TypeKind.Interface => EngineeringTypeKind.Interface,
                TypeKind.Struct => EngineeringTypeKind.Struct,
                TypeKind.Enum => EngineeringTypeKind.Enum,
                TypeKind.Delegate => EngineeringTypeKind.Delegate,
                _ => EngineeringTypeKind.Class
            };
        }

        private static EngineeringAccessibility MapEngineeringAccessibility(Accessibility accessibility)
        {
            return accessibility switch
            {
                Accessibility.Public => EngineeringAccessibility.Public,
                Accessibility.Internal => EngineeringAccessibility.Internal,
                Accessibility.Protected => EngineeringAccessibility.Protected,
                Accessibility.Private => EngineeringAccessibility.Private,
                _ => EngineeringAccessibility.Unknown
            };
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
                                Kind = MapEngineeringTypeKind(symbol.TypeKind),
                                Accessibility = MapEngineeringAccessibility(symbol.DeclaredAccessibility),
                                IsAbstract = symbol.IsAbstract,
                                IsStatic = symbol.IsStatic,
                                IsGeneric = symbol.IsGenericType,
                                GenericParameterCount = symbol.TypeParameters.Length,
                                GenericConstraintCount = symbol.TypeParameters.Sum(p => p.ConstraintTypes.Length),
                                IsSealed = symbol.IsSealed,
                                ImplementedInterfaceCount = symbol.Interfaces.Length,
                                AttributeCount = symbol.GetAttributes().Length,
                                NestedTypeCount = symbol.GetTypeMembers().Length,
                                SourceLineCount = GetSourceLineCount(tree, decl),
                                DependencyCount = 0, // heuristic: not computed here
                                BaseType = symbol.BaseType?.ToDisplayString(),
                                BaseTypeReference = null,
                                MethodCount = symbol.GetMembers().OfType<IMethodSymbol>().Count(m => m.MethodKind == MethodKind.Ordinary),
                                ConstructorCount = symbol.Constructors.Length,
                                PropertyCount = symbol.GetMembers().OfType<IPropertySymbol>().Count(),
                                FieldCount = symbol.GetMembers().OfType<IFieldSymbol>().Count(),
                                EventCount = symbol.GetMembers().OfType<IEventSymbol>().Count(),
                                SourceFilePath = tree.FilePath
                            };

                            // Capture implemented interface names
                            try
                            {
                                foreach (var iface in symbol.Interfaces)
                                {
                                    try
                                    {
                                        var disp = iface.ToDisplayString();
                                        td.ImplementedInterfaces.Add(disp);
                                        // If the interface is defined in the same compilation, produce a canonical QualifiedName
                                        try
                                        {
                                            if (iface.DeclaringSyntaxReferences != null && iface.DeclaringSyntaxReferences.Length > 0)
                                            {
                                                var ifaceNs = iface.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                                                var ifaceName = iface.Name ?? string.Empty;
                                                var ifaceQn = !string.IsNullOrWhiteSpace(projectName)
                                                    ? (!string.IsNullOrWhiteSpace(ifaceNs) ? $"{projectName}:{ifaceNs}.{ifaceName}" : $"{projectName}:{ifaceName}")
                                                    : (!string.IsNullOrWhiteSpace(ifaceNs) ? $"{ifaceNs}.{ifaceName}" : $"{tree.FilePath}:{ifaceName}");
                                                // Attach a minimal canonical reference via BaseTypeReference on TypeDescriptor only for discovery to map
                                                // We'll rely on Discovery to convert display lists to TypeReference objects using project-local knowledge.
                                                // For interface lists we do not store TypeReference here to avoid exposing Core types; keep discovery responsible.
                                            }
                                        }
                                        catch { }
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            // Capture constructor parameter types
                            try
                            {
                                foreach (var ctor in symbol.InstanceConstructors)
                                {
                                    foreach (var p in ctor.Parameters)
                                    {
                                        try { td.ConstructorParameterTypes.Add(p.Type.ToDisplayString()); } catch { }
                                    }
                                }
                            }
                            catch { }

                            // Capture member-level details into MemberDescriptor objects (provider-owned)
                            try
                            {
                                // Methods (ordinary)
                                foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
                                {
                                    try
                                    {
                                        var md = new MemberDescriptor
                                        {
                                            Project = projectName ?? string.Empty,
                                            Namespace = ns ?? string.Empty,
                                            TypeName = typeName ?? string.Empty,
                                            MemberName = method.Name ?? string.Empty,
                                            Kind = MemberKind.Method,
                                            Visibility = method.DeclaredAccessibility.ToString(),
                                            IsStatic = method.IsStatic,
                                            IsAsync = method.IsAsync,
                                            LineCount = 0
                                        };

                                        try { md.ReturnTypeDisplay = method.ReturnType?.ToDisplayString(); } catch { }

                                        try
                                        {
                                            foreach (var p in method.Parameters)
                                            {
                                                try { md.ParameterTypeDisplays.Add(p.Type.ToDisplayString()); } catch { }
                                            }
                                        }
                                        catch { }

                                        try
                                        {
                                            var returnNamed = method.ReturnType as INamedTypeSymbol;
                                            if (returnNamed != null && returnNamed.IsGenericType)
                                            {
                                                foreach (var ta in returnNamed.TypeArguments)
                                                {
                                                    try { md.GenericArgumentDisplays.Add(ta.ToDisplayString()); } catch { }
                                                }
                                            }
                                        }
                                        catch { }

                                        ctx.MemberDescriptors.Add(md);
                                    }
                                    catch { }
                                }

                                // Constructors
                                foreach (var ctor in symbol.InstanceConstructors)
                                {
                                    try
                                    {
                                        var md = new MemberDescriptor
                                        {
                                            Project = projectName ?? string.Empty,
                                            Namespace = ns ?? string.Empty,
                                            TypeName = typeName ?? string.Empty,
                                            MemberName = ctor.Name ?? string.Empty,
                                            Kind = MemberKind.Constructor,
                                            Visibility = ctor.DeclaredAccessibility.ToString(),
                                            IsStatic = ctor.IsStatic,
                                            IsAsync = false,
                                            LineCount = 0
                                        };

                                        try
                                        {
                                            foreach (var p in ctor.Parameters)
                                            {
                                                try { md.ParameterTypeDisplays.Add(p.Type.ToDisplayString()); } catch { }
                                            }
                                        }
                                        catch { }

                                        ctx.MemberDescriptors.Add(md);
                                    }
                                    catch { }
                                }

                                // Properties
                                foreach (var prop in symbol.GetMembers().OfType<IPropertySymbol>())
                                {
                                    try
                                    {
                                        var md = new MemberDescriptor
                                        {
                                            Project = projectName ?? string.Empty,
                                            Namespace = ns ?? string.Empty,
                                            TypeName = typeName ?? string.Empty,
                                            MemberName = prop.Name ?? string.Empty,
                                            Kind = MemberKind.Property,
                                            Visibility = prop.DeclaredAccessibility.ToString(),
                                            IsStatic = prop.IsStatic,
                                            IsAsync = false,
                                            LineCount = 0
                                        };

                                        try { md.ReturnTypeDisplay = prop.Type?.ToDisplayString(); } catch { }
                                        ctx.MemberDescriptors.Add(md);
                                    }
                                    catch { }
                                }

                                // Fields
                                foreach (var f in symbol.GetMembers().OfType<IFieldSymbol>())
                                {
                                    try
                                    {
                                        var md = new MemberDescriptor
                                        {
                                            Project = projectName ?? string.Empty,
                                            Namespace = ns ?? string.Empty,
                                            TypeName = typeName ?? string.Empty,
                                            MemberName = f.Name ?? string.Empty,
                                            Kind = MemberKind.Field,
                                            Visibility = f.DeclaredAccessibility.ToString(),
                                            IsStatic = f.IsStatic,
                                            IsAsync = false,
                                            LineCount = 0
                                        };

                                        try { md.ReturnTypeDisplay = f.Type?.ToDisplayString(); } catch { }
                                        ctx.MemberDescriptors.Add(md);
                                    }
                                    catch { }
                                }

                                // Events
                                foreach (var ev in symbol.GetMembers().OfType<IEventSymbol>())
                                {
                                    try
                                    {
                                        var md = new MemberDescriptor
                                        {
                                            Project = projectName ?? string.Empty,
                                            Namespace = ns ?? string.Empty,
                                            TypeName = typeName ?? string.Empty,
                                            MemberName = ev.Name ?? string.Empty,
                                            Kind = MemberKind.Event,
                                            Visibility = ev.DeclaredAccessibility.ToString(),
                                            IsStatic = ev.IsStatic,
                                            IsAsync = false,
                                            LineCount = 0
                                        };

                                        try { md.ReturnTypeDisplay = ev.Type?.ToDisplayString(); } catch { }
                                        ctx.MemberDescriptors.Add(md);
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            // Capture method parameter types and generic argument types
                            try
                            {
                                foreach (var method in symbol.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary))
                                {
                                    foreach (var p in method.Parameters)
                                    {
                                        try { td.MethodParameterTypes.Add(p.Type.ToDisplayString()); } catch { }
                                    }

                                    // Generic type arguments from method return type or parameters
                                    try
                                    {
                                        var returnType = method.ReturnType as INamedTypeSymbol;
                                        if (returnType != null && returnType.IsGenericType)
                                        {
                                            foreach (var ta in returnType.TypeArguments)
                                            {
                                                try { td.GenericArgumentTypes.Add(ta.ToDisplayString()); } catch { }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            // Capture field, property, and event types
                            try
                            {
                                foreach (var f in symbol.GetMembers().OfType<IFieldSymbol>())
                                {
                                    try { td.FieldTypes.Add(f.Type.ToDisplayString()); } catch { }
                                }
                                foreach (var p in symbol.GetMembers().OfType<IPropertySymbol>())
                                {
                                    try { td.PropertyTypes.Add(p.Type.ToDisplayString()); } catch { }
                                }
                                foreach (var ev in symbol.GetMembers().OfType<IEventSymbol>())
                                {
                                    try { td.EventTypes.Add(ev.Type.ToDisplayString()); } catch { }
                                }
                            }
                            catch { }

                            ctx.Types.Add(td);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return ctx;
        }

        private static int GetSourceLineCount(Microsoft.CodeAnalysis.SyntaxTree tree, TypeDeclarationSyntax decl)
        {
            try
            {
                var span = decl.Span;
                var startLine = tree.GetLineSpan(span).StartLinePosition.Line;
                var endLine = tree.GetLineSpan(span).EndLinePosition.Line;
                return Math.Max(0, endLine - startLine + 1);
            }
            catch
            {
                return 0;
            }
        }
    }
}
