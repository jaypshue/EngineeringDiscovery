using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal class JavaRepositoryProvider : IRepositoryProvider, ILanguageProvider
    {
        private static readonly Regex PackageDeclarationRegex = new Regex(@"^\s*package\s+([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*;", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex TypeDeclarationRegex = new Regex(@"^\s*(?:(public|protected|private)\s+)?(?:(abstract|final|static)\s+)*(class|interface|enum|record)\s+([A-Za-z_][A-Za-z0-9_]*)(?<clauses>[^\{;]*)", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex MemberDeclarationRegex = new Regex(@"^(?:(?<visibility>public|protected|private)\s+)?(?:(?<modifier>static|abstract|final|synchronized|native|strictfp|default)\s+)*(?:(?<typeParams><[^>]+>)\s+)?(?:(?<returnType>[A-Za-z_$][A-Za-z0-9_$.]*(?:\s*<[^;{}()]+>)?(?:\s*\[\])?(?:\s*\.\.\.)?)\s+)?(?<name>[A-Za-z_$][A-Za-z0-9_$]*)\s*\((?<parameters>[^)]*)\)\s*(?:throws\s+[^{;]+)?$", RegexOptions.Compiled | RegexOptions.Singleline);

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

            foreach (var memberDescriptor in DiscoverMembers(context.ProjectName, layout.JavaSourceFiles))
            {
                context.MemberDescriptors.Add(memberDescriptor);
            }

            ApplyMemberCounts(context);

            return new[] { context };
        }

        private static void ApplyMemberCounts(CompilationContext context)
        {
            foreach (var type in context.Types)
            {
                var members = context.MemberDescriptors
                    .Where(member => string.Equals(member.Namespace ?? string.Empty, type.Namespace ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                                     string.Equals(member.TypeName ?? string.Empty, type.TypeName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                type.MethodCount = members.Count(member => member.Kind == MemberKind.Method);
                type.ConstructorCount = members.Count(member => member.Kind == MemberKind.Constructor);
                type.FieldCount = members.Count(member => member.Kind == MemberKind.Field);

                foreach (var constructorParameterType in members
                    .Where(member => member.Kind == MemberKind.Constructor)
                    .SelectMany(member => member.ParameterTypeDisplays)
                    .Where(parameterType => !string.IsNullOrWhiteSpace(parameterType)))
                {
                    type.ConstructorParameterTypes.Add(constructorParameterType);
                }

                foreach (var methodParameterType in members
                    .Where(member => member.Kind == MemberKind.Method)
                    .SelectMany(member => member.ParameterTypeDisplays)
                    .Where(parameterType => !string.IsNullOrWhiteSpace(parameterType)))
                {
                    type.MethodParameterTypes.Add(methodParameterType);
                }

                foreach (var fieldType in members
                    .Where(member => member.Kind == MemberKind.Field && !string.IsNullOrWhiteSpace(member.ReturnTypeDisplay))
                    .Select(member => member.ReturnTypeDisplay!))
                {
                    type.FieldTypes.Add(fieldType);
                }
            }
        }

        private static IEnumerable<MemberDescriptor> DiscoverMembers(string projectName, IEnumerable<string> javaSourceFiles)
        {
            foreach (var javaFile in javaSourceFiles)
            {
                string text;
                try { text = File.ReadAllText(javaFile); }
                catch { continue; }

                var packageName = GetPackageName(text);
                if (string.IsNullOrWhiteSpace(packageName)) continue;

                var sanitizedText = SanitizeJavaText(text);
                foreach (Match typeMatch in TypeDeclarationRegex.Matches(sanitizedText))
                {
                    if (!typeMatch.Success) continue;
                    if (GetBraceDepth(sanitizedText, typeMatch.Index) != 0) continue;

                    var typeName = typeMatch.Groups[4].Value.Trim();
                    if (string.IsNullOrWhiteSpace(typeName)) continue;

                    var typeBodyStart = sanitizedText.IndexOf('{', typeMatch.Index + typeMatch.Length);
                    if (typeBodyStart < 0) continue;

                    var typeBodyEnd = FindMatchingBrace(sanitizedText, typeBodyStart);
                    if (typeBodyEnd <= typeBodyStart) continue;

                    foreach (var memberDescriptor in DiscoverMembersInType(projectName, packageName, typeName, javaFile, sanitizedText, typeBodyStart + 1, typeBodyEnd))
                    {
                        yield return memberDescriptor;
                    }
                }
            }
        }

        private static IEnumerable<MemberDescriptor> DiscoverMembersInType(string projectName, string packageName, string typeName, string javaFile, string text, int bodyStart, int bodyEnd)
        {
            var declarationStart = bodyStart;
            var depth = 0;

            for (var i = bodyStart; i < bodyEnd; i++)
            {
                var ch = text[i];
                if (ch == '{')
                {
                    if (depth == 0)
                    {
                        var header = text.Substring(declarationStart, i - declarationStart).Trim();
                        if (TryCreateMethodOrConstructor(projectName, packageName, typeName, javaFile, header, true, out var descriptor))
                        {
                            descriptor.LineCount = CountLines(text, declarationStart, FindMatchingBrace(text, i));
                            yield return descriptor;
                        }
                    }

                    depth++;
                    continue;
                }

                if (ch == '}')
                {
                    if (depth > 0) depth--;
                    if (depth == 0) declarationStart = i + 1;
                    continue;
                }

                if (ch == ';' && depth == 0)
                {
                    var declaration = text.Substring(declarationStart, i - declarationStart).Trim();
                    if (TryCreateMethodOrConstructor(projectName, packageName, typeName, javaFile, declaration, false, out var descriptor))
                    {
                        yield return descriptor;
                    }
                    else
                    {
                        foreach (var fieldDescriptor in TryCreateFieldDescriptors(projectName, packageName, typeName, javaFile, declaration))
                        {
                            yield return fieldDescriptor;
                        }
                    }

                    declarationStart = i + 1;
                }
            }
        }

        private static bool TryCreateMethodOrConstructor(string projectName, string packageName, string typeName, string javaFile, string declaration, bool hasBody, out MemberDescriptor descriptor)
        {
            descriptor = null!;
            declaration = NormalizeDeclaration(declaration);
            if (string.IsNullOrWhiteSpace(declaration)) return false;
            if (IsTypeDeclaration(declaration)) return false;
            if (!declaration.Contains('(') || !declaration.Contains(')')) return false;

            var match = MemberDeclarationRegex.Match(declaration);
            if (!match.Success) return false;

            var memberName = match.Groups["name"].Value.Trim();
            if (string.IsNullOrWhiteSpace(memberName)) return false;

            var returnType = match.Groups["returnType"].Value.Trim();
            var isConstructor = string.Equals(memberName, typeName, StringComparison.Ordinal);
            if (!isConstructor && string.IsNullOrWhiteSpace(returnType)) return false;

            descriptor = new MemberDescriptor
            {
                Project = projectName,
                Namespace = packageName,
                TypeName = typeName,
                MemberName = memberName,
                Kind = isConstructor ? MemberKind.Constructor : MemberKind.Method,
                Visibility = MapJavaMemberVisibility(match.Groups["visibility"].Value),
                IsStatic = ContainsModifier(declaration, "static"),
                IsAbstract = ContainsModifier(declaration, "abstract") || (!hasBody && !isConstructor),
                IsSealed = ContainsModifier(declaration, "final"),
                IsAsync = false,
                ReturnTypeDisplay = isConstructor ? null : returnType,
                SourceFilePath = javaFile
            };

            foreach (var parameterType in ParseParameterTypes(match.Groups["parameters"].Value))
            {
                descriptor.ParameterTypeDisplays.Add(parameterType);
            }

            return true;
        }

        private static IEnumerable<MemberDescriptor> TryCreateFieldDescriptors(string projectName, string packageName, string typeName, string javaFile, string declaration)
        {
            declaration = NormalizeDeclaration(declaration);
            if (string.IsNullOrWhiteSpace(declaration)) yield break;
            if (IsTypeDeclaration(declaration)) yield break;
            if (declaration.Contains('(') || declaration.Contains(')')) yield break;

            var tokens = SplitWhitespace(declaration).ToList();
            if (tokens.Count < 2) yield break;

            var visibility = "";
            if (IsVisibilityModifier(tokens[0]))
            {
                visibility = tokens[0];
                tokens.RemoveAt(0);
            }

            var isStatic = false;
            var isFinal = false;
            while (tokens.Count > 0 && IsJavaFieldModifier(tokens[0]))
            {
                isStatic |= string.Equals(tokens[0], "static", StringComparison.OrdinalIgnoreCase);
                isFinal |= string.Equals(tokens[0], "final", StringComparison.OrdinalIgnoreCase);
                tokens.RemoveAt(0);
            }

            if (tokens.Count < 2) yield break;

            var remainder = string.Join(" ", tokens);
            var declarators = SplitTopLevel(remainder, ',').ToList();
            if (declarators.Count == 0) yield break;

            var firstDeclarator = RemoveInitializer(declarators[0]).Trim();
            var firstParts = SplitWhitespace(firstDeclarator).ToList();
            if (firstParts.Count < 2) yield break;

            var declaredType = string.Join(" ", firstParts.Take(firstParts.Count - 1)).Trim();
            if (string.IsNullOrWhiteSpace(declaredType)) yield break;

            for (var i = 0; i < declarators.Count; i++)
            {
                var declarator = RemoveInitializer(declarators[i]).Trim();
                var name = i == 0 ? firstParts.LastOrDefault() ?? string.Empty : SplitWhitespace(declarator).FirstOrDefault() ?? string.Empty;
                name = NormalizeFieldName(name);
                if (string.IsNullOrWhiteSpace(name)) continue;

                yield return new MemberDescriptor
                {
                    Project = projectName,
                    Namespace = packageName,
                    TypeName = typeName,
                    MemberName = name,
                    Kind = MemberKind.Field,
                    Visibility = MapJavaMemberVisibility(visibility),
                    IsStatic = isStatic,
                    IsAbstract = false,
                    IsSealed = isFinal,
                    IsAsync = false,
                    ReturnTypeDisplay = declaredType,
                    SourceFilePath = javaFile
                };
            }
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
                    var typeDescriptor = new TypeDescriptor
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

                    PopulateJavaTypeRelationships(typeDescriptor, match.Groups["clauses"].Value);

                    yield return typeDescriptor;
                }
            }
        }

        private static void PopulateJavaTypeRelationships(TypeDescriptor typeDescriptor, string clauses)
        {
            if (typeDescriptor == null || string.IsNullOrWhiteSpace(clauses)) return;

            var extendsMatch = Regex.Match(clauses, @"\bextends\s+(?<types>.*?)(?=\bimplements\b|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (extendsMatch.Success)
            {
                var extendedTypes = ParseJavaTypeList(extendsMatch.Groups["types"].Value).ToList();
                if (typeDescriptor.Kind == EngineeringTypeKind.Interface)
                {
                    foreach (var extendedInterface in extendedTypes)
                    {
                        typeDescriptor.ImplementedInterfaces.Add(extendedInterface);
                    }
                }
                else
                {
                    typeDescriptor.BaseType = extendedTypes.FirstOrDefault();
                }
            }

            var implementsMatch = Regex.Match(clauses, @"\bimplements\s+(?<types>.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (implementsMatch.Success)
            {
                foreach (var implementedInterface in ParseJavaTypeList(implementsMatch.Groups["types"].Value))
                {
                    typeDescriptor.ImplementedInterfaces.Add(implementedInterface);
                }
            }

            typeDescriptor.ImplementedInterfaceCount = typeDescriptor.ImplementedInterfaces
                .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        private static IEnumerable<string> ParseJavaTypeList(string text)
        {
            foreach (var item in SplitTopLevel(text ?? string.Empty, ','))
            {
                var normalized = NormalizeJavaTypeName(item);
                if (!string.IsNullOrWhiteSpace(normalized)) yield return normalized;
            }
        }

        private static string NormalizeJavaTypeName(string typeName)
        {
            typeName = (typeName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(typeName)) return string.Empty;

            var genericStart = typeName.IndexOf('<');
            if (genericStart >= 0) typeName = typeName.Substring(0, genericStart).Trim();
            typeName = typeName.Replace("...", string.Empty, StringComparison.Ordinal).Trim();
            while (typeName.EndsWith("[]", StringComparison.Ordinal)) typeName = typeName.Substring(0, typeName.Length - 2).Trim();

            return Regex.Match(typeName, @"[A-Za-z_$][A-Za-z0-9_$.]*").Value;
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

        private static string SanitizeJavaText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '/' && i + 1 < chars.Length && chars[i + 1] == '/')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i += 2;
                    while (i < chars.Length && chars[i] != '\r' && chars[i] != '\n')
                    {
                        chars[i++] = ' ';
                    }
                }
                else if (chars[i] == '/' && i + 1 < chars.Length && chars[i + 1] == '*')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i += 2;
                    while (i + 1 < chars.Length && !(chars[i] == '*' && chars[i + 1] == '/'))
                    {
                        if (chars[i] != '\r' && chars[i] != '\n') chars[i] = ' ';
                        i++;
                    }

                    if (i + 1 < chars.Length)
                    {
                        chars[i] = ' ';
                        chars[i + 1] = ' ';
                    }
                }
                else if (chars[i] == '"' || chars[i] == '\'')
                {
                    var quote = chars[i];
                    i++;
                    while (i < chars.Length)
                    {
                        if (chars[i] == '\\')
                        {
                            i += 2;
                            continue;
                        }

                        if (chars[i] == quote) break;
                        if (chars[i] != '\r' && chars[i] != '\n') chars[i] = ' ';
                        i++;
                    }
                }
            }

            return new string(chars);
        }

        private static int FindMatchingBrace(string text, int openBraceIndex)
        {
            var depth = 0;
            for (var i = openBraceIndex; i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }

            return -1;
        }

        private static int GetBraceDepth(string text, int endExclusive)
        {
            var depth = 0;
            for (var i = 0; i < endExclusive && i < text.Length; i++)
            {
                if (text[i] == '{') depth++;
                else if (text[i] == '}' && depth > 0) depth--;
            }

            return depth;
        }

        private static int CountLines(string text, int start, int end)
        {
            if (start < 0 || end <= start || start >= text.Length) return 0;
            end = Math.Min(end, text.Length - 1);
            var count = 1;
            for (var i = start; i <= end; i++)
            {
                if (text[i] == '\n') count++;
            }

            return count;
        }

        private static string NormalizeDeclaration(string declaration)
        {
            if (string.IsNullOrWhiteSpace(declaration)) return string.Empty;

            var lines = declaration
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !line.StartsWith("@", StringComparison.Ordinal));

            return string.Join(" ", lines).Trim();
        }

        private static bool IsTypeDeclaration(string declaration)
        {
            return Regex.IsMatch(declaration ?? string.Empty, @"\b(class|interface|enum|record)\b", RegexOptions.IgnoreCase);
        }

        private static string MapJavaMemberVisibility(string modifier)
        {
            return modifier.ToLowerInvariant() switch
            {
                "public" => "Public",
                "protected" => "Protected",
                "private" => "Private",
                _ => "Internal"
            };
        }

        private static IEnumerable<string> ParseParameterTypes(string parameters)
        {
            foreach (var parameter in SplitTopLevel(parameters ?? string.Empty, ','))
            {
                var normalized = NormalizeDeclaration(parameter);
                if (string.IsNullOrWhiteSpace(normalized)) continue;

                normalized = Regex.Replace(normalized, @"\bfinal\s+", string.Empty, RegexOptions.IgnoreCase);
                normalized = Regex.Replace(normalized, @"@\S+", string.Empty).Trim();
                var parts = SplitWhitespace(normalized).ToList();
                if (parts.Count < 2) continue;

                var type = string.Join(" ", parts.Take(parts.Count - 1)).Trim();
                if (!string.IsNullOrWhiteSpace(type)) yield return type;
            }
        }

        private static IEnumerable<string> SplitTopLevel(string text, char separator)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;

            var start = 0;
            var angleDepth = 0;
            var parenDepth = 0;
            var bracketDepth = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '<') angleDepth++;
                else if (ch == '>' && angleDepth > 0) angleDepth--;
                else if (ch == '(') parenDepth++;
                else if (ch == ')' && parenDepth > 0) parenDepth--;
                else if (ch == '[') bracketDepth++;
                else if (ch == ']' && bracketDepth > 0) bracketDepth--;
                else if (ch == separator && angleDepth == 0 && parenDepth == 0 && bracketDepth == 0)
                {
                    yield return text.Substring(start, i - start).Trim();
                    start = i + 1;
                }
            }

            yield return text.Substring(start).Trim();
        }

        private static IEnumerable<string> SplitWhitespace(string text)
        {
            return Regex.Split(text.Trim(), @"\s+").Where(part => !string.IsNullOrWhiteSpace(part));
        }

        private static string RemoveInitializer(string declarator)
        {
            var depth = 0;
            for (var i = 0; i < declarator.Length; i++)
            {
                var ch = declarator[i];
                if (ch == '<' || ch == '(' || ch == '[' || ch == '{') depth++;
                else if ((ch == '>' || ch == ')' || ch == ']' || ch == '}') && depth > 0) depth--;
                else if (ch == '=' && depth == 0) return declarator.Substring(0, i);
            }

            return declarator;
        }

        private static string NormalizeFieldName(string name)
        {
            name = (name ?? string.Empty).Trim();
            while (name.EndsWith("[]", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 2).Trim();
            return Regex.Match(name, @"[A-Za-z_$][A-Za-z0-9_$]*").Value;
        }

        private static bool IsVisibilityModifier(string token)
        {
            return string.Equals(token, "public", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "protected", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "private", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsJavaFieldModifier(string token)
        {
            return string.Equals(token, "static", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "final", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "transient", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "volatile", StringComparison.OrdinalIgnoreCase);
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
