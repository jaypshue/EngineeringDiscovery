using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal class JavaRepositoryProvider : IRepositoryProvider
    {
        private static readonly Regex PackageDeclarationRegex = new Regex(@"^\s*package\s+([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*;", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex TypeDeclarationRegex = new Regex(@"^\s*(?:(public|protected|private)\s+)?(?:(abstract|final|static)\s+)*(class|interface|enum|record)\s+([A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly string[] BuildFileNames =
        {
            "pom.xml",
            "build.gradle",
            "build.gradle.kts"
        };

        private static readonly string[] ExcludedDirectoryNames =
        {
            ".git",
            ".gradle",
            ".idea",
            ".vs",
            "bin",
            "build",
            "node_modules",
            "obj",
            "out",
            "target"
        };

        public bool CanLoad(string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot)) return false;

            return EnumerateBuildFiles(repositoryRoot).Any();
        }

        public IReadOnlyList<CompilationContext> Load(string repositoryRoot)
        {
            if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot)) return Array.Empty<CompilationContext>();

            var buildFiles = EnumerateBuildFiles(repositoryRoot).ToList();
            if (buildFiles.Count == 0) return Array.Empty<CompilationContext>();

            var moduleDirectories = buildFiles
                .Select(path => Path.GetDirectoryName(path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(path!))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sourceRoots = new List<SourceRootDescriptor>();
            var javaFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var moduleDirectory in moduleDirectories)
            {
                AddSourceRoot(moduleDirectory, "src", "main", "java", false, sourceRoots, javaFiles);
                AddSourceRoot(moduleDirectory, "src", "test", "java", true, sourceRoots, javaFiles);
            }

            var layout = new JavaRepositoryLayout
            {
                RepositoryRoot = Path.GetFullPath(repositoryRoot),
                BuildSystem = DetermineBuildSystem(buildFiles)
            };

            foreach (var moduleDirectory in moduleDirectories)
            {
                layout.Modules.Add(moduleDirectory);
            }

            foreach (var sourceRoot in sourceRoots
                .DistinctBy(root => root.Path, StringComparer.OrdinalIgnoreCase)
                .OrderBy(root => root.Path, StringComparer.OrdinalIgnoreCase))
            {
                layout.SourceRoots.Add(sourceRoot);
            }

            foreach (var javaFile in javaFiles)
            {
                layout.JavaSourceFiles.Add(javaFile);
            }

            var context = new CompilationContext
            {
                Language = RepositoryLanguage.Java,
                ProjectName = Path.GetFileName(Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                ProjectFilePath = buildFiles.FirstOrDefault(),
                RepositoryRoot = Path.GetFullPath(repositoryRoot),
                JavaLayout = layout
            };

            foreach (var namespaceObservation in DiscoverNamespaces(context.ProjectName, layout.JavaSourceFiles))
            {
                context.NamespaceObservations.Add(namespaceObservation);
            }

            foreach (var typeDescriptor in DiscoverTypes(context.ProjectName, layout.JavaSourceFiles))
            {
                context.Types.Add(typeDescriptor);
            }

            return new[] { context };
        }

        private static IEnumerable<TypeDescriptor> DiscoverTypes(string projectName, IEnumerable<string> javaSourceFiles)
        {
            foreach (var javaFile in javaSourceFiles)
            {
                string text;
                try { text = File.ReadAllText(javaFile); }
                catch { continue; }

                var packageName = GetPackageName(text);
                if (string.IsNullOrWhiteSpace(packageName)) continue;

                foreach (Match match in TypeDeclarationRegex.Matches(text))
                {
                    if (!match.Success) continue;

                    var keyword = match.Groups[3].Value.Trim();
                    var typeName = match.Groups[4].Value.Trim();
                    if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(typeName)) continue;

                    var declarationPrefix = match.Value;

                    yield return new TypeDescriptor
                    {
                        Namespace = packageName,
                        TypeName = typeName,
                        QualifiedName = $"{projectName}:{packageName}.{typeName}",
                        Kind = MapJavaTypeKind(keyword),
                        Accessibility = MapJavaAccessibility(match.Groups[1].Value),
                        IsAbstract = ContainsModifier(declarationPrefix, "abstract") || string.Equals(keyword, "interface", StringComparison.OrdinalIgnoreCase),
                        IsSealed = ContainsModifier(declarationPrefix, "final"),
                        IsStatic = ContainsModifier(declarationPrefix, "static"),
                        SourceFilePath = javaFile
                    };
                }
            }
        }

        private static IEnumerable<NamespaceObservation> DiscoverNamespaces(string projectName, IEnumerable<string> javaSourceFiles)
        {
            var packages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var javaFile in javaSourceFiles)
            {
                try
                {
                    var text = File.ReadAllText(javaFile);
                    var match = PackageDeclarationRegex.Match(text);
                    if (match.Success)
                    {
                        var packageName = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(packageName)) packages.Add(packageName);
                    }
                }
                catch { }
            }

            foreach (var packageName in packages)
            {
                yield return new NamespaceObservation
                {
                    Project = projectName,
                    NamespaceName = packageName
                };
            }
        }

        private static string GetPackageName(string text)
        {
            var match = PackageDeclarationRegex.Match(text);
            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }

        private static EngineeringTypeKind MapJavaTypeKind(string keyword)
        {
            return keyword.ToLowerInvariant() switch
            {
                "class" => EngineeringTypeKind.Class,
                "interface" => EngineeringTypeKind.Interface,
                "enum" => EngineeringTypeKind.Enum,
                "record" => EngineeringTypeKind.Record,
                _ => EngineeringTypeKind.Unknown
            };
        }

        private static EngineeringAccessibility MapJavaAccessibility(string modifier)
        {
            return modifier.ToLowerInvariant() switch
            {
                "public" => EngineeringAccessibility.Public,
                "protected" => EngineeringAccessibility.Protected,
                "private" => EngineeringAccessibility.Private,
                _ => EngineeringAccessibility.Package
            };
        }

        private static bool ContainsModifier(string declarationPrefix, string modifier)
        {
            return Regex.IsMatch(declarationPrefix ?? string.Empty, $@"\b{Regex.Escape(modifier)}\b", RegexOptions.IgnoreCase);
        }

        private static IEnumerable<string> EnumerateBuildFiles(string repositoryRoot)
        {
            foreach (var fileName in BuildFileNames)
            {
                foreach (var file in EnumerateFiles(repositoryRoot, fileName))
                {
                    yield return file;
                }
            }
        }

        private static IEnumerable<string> EnumerateFiles(string root, string searchPattern)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();

                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(current, searchPattern, SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (var file in files)
                {
                    yield return Path.GetFullPath(file);
                }

                IEnumerable<string> directories;
                try { directories = Directory.EnumerateDirectories(current); }
                catch { continue; }

                foreach (var directory in directories)
                {
                    if (!IsExcludedDirectory(directory)) pending.Push(directory);
                }
            }
        }

        private static void AddSourceRoot(string moduleDirectory, string src, string scope, string language, bool isTestSource, List<SourceRootDescriptor> sourceRoots, SortedSet<string> javaFiles)
        {
            var sourceRoot = Path.Combine(moduleDirectory, src, scope, language);
            if (!Directory.Exists(sourceRoot)) return;

            sourceRoots.Add(new SourceRootDescriptor
            {
                Path = Path.GetFullPath(sourceRoot),
                IsTestSource = isTestSource
            });

            foreach (var javaFile in EnumerateFiles(sourceRoot, "*.java"))
            {
                javaFiles.Add(javaFile);
            }
        }

        private static JavaBuildSystem DetermineBuildSystem(IReadOnlyCollection<string> buildFiles)
        {
            if (buildFiles.Any(file => string.Equals(Path.GetFileName(file), "pom.xml", StringComparison.OrdinalIgnoreCase)))
            {
                return JavaBuildSystem.Maven;
            }

            if (buildFiles.Any(file =>
                string.Equals(Path.GetFileName(file), "build.gradle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(file), "build.gradle.kts", StringComparison.OrdinalIgnoreCase)))
            {
                return JavaBuildSystem.Gradle;
            }

            return JavaBuildSystem.Unknown;
        }

        private static bool IsExcludedDirectory(string path)
        {
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return ExcludedDirectoryNames.Any(excluded => string.Equals(name, excluded, StringComparison.OrdinalIgnoreCase));
        }
    }
}
