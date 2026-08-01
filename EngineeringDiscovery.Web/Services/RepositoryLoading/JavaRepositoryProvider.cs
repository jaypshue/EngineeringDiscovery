using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal class JavaRepositoryProvider : IRepositoryProvider
    {
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

            return new[] { context };
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
