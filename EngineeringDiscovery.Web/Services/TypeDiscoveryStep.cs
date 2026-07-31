using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class TypeDiscoveryStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Discovery;
        private readonly Investigation _investigation;

        public TypeDiscoveryStep(Investigation investigation)
        {
            _investigation = investigation ?? throw new ArgumentNullException(nameof(investigation));
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            foreach (var proj in context.DiscoveredProjects)
            {
                try
                {
                    var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                    if (string.IsNullOrWhiteSpace(proj.Path) || !File.Exists(proj.Path)) continue;

                    var projectFolder = Path.GetDirectoryName(proj.Path) ?? string.Empty;
                    var doc = System.Xml.Linq.XDocument.Load(proj.Path);
                    var rootNamespace = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "RootNamespace", StringComparison.OrdinalIgnoreCase))?.Value;

                    if (!string.IsNullOrWhiteSpace(projectFolder) && Directory.Exists(projectFolder))
                    {
                        var csFilesAll = Directory.GetFiles(projectFolder, "*.cs", SearchOption.AllDirectories);
                        var typeRegex = new Regex("\\b(class|interface|record(?:\\s+class|\\s+struct)?|struct|enum|delegate)\\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
                        var nsRegexPerFile = new Regex("\\bnamespace\\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);

                        foreach (var csf in csFilesAll)
                        {
                            try
                            {
                                var text = File.ReadAllText(csf);
                                var nsMatch = nsRegexPerFile.Match(text);
                                var fileNs = nsMatch.Success ? nsMatch.Groups[1].Value.Trim() : (rootNamespace ?? string.Empty);
                                var matches = typeRegex.Matches(text);

                                string ExtractBalancedBlock(string source, int startIndex)
                                {
                                    var openIndex = source.IndexOf('{', startIndex);
                                    if (openIndex < 0) return string.Empty;
                                    var depth = 1;
                                    for (var i = openIndex + 1; i < source.Length; i++)
                                    {
                                        var ch = source[i];
                                        if (ch == '{') depth++;
                                        else if (ch == '}') depth--;
                                        if (depth == 0)
                                        {
                                            return source.Substring(openIndex, i - openIndex + 1);
                                        }
                                    }
                                    return string.Empty;
                                }

                                foreach (System.Text.RegularExpressions.Match m in matches)
                                {
                                    var kindRaw = m.Groups[1].Value.Trim();
                                    var typeName = m.Groups[2].Value.Trim();
                                    string kind;
                                    if (kindRaw.StartsWith("record", StringComparison.OrdinalIgnoreCase)) kind = "record";
                                    else kind = kindRaw.ToLowerInvariant();

                                    switch (kind)
                                    {
                                        case "class": break;
                                        case "interface": break;
                                        case "record": break;
                                        case "struct": break;
                                        case "enum": break;
                                        case "delegate": break;
                                    }

                                    var fileNameOnly = Path.GetFileName(csf);
                                    var desc = $"Project '{name}' defines {kind} '{typeName}' in namespace '{(string.IsNullOrWhiteSpace(fileNs) ? "<global>" : fileNs)}' (file: {fileNameOnly}).";
                                    _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, desc));
                                    _investigation.AddObservation(new DiscoveryObservation
                                    {
                                        Kind = ObservationKind.Type,
                                        Project = name,
                                        Namespace = fileNs,
                                        Type = typeName,
                                        Description = desc
                                    });

                                    try
                                    {
                                        var typeBody = ExtractBalancedBlock(text, m.Index);
                                        var declEnd = text.IndexOf('{', m.Index);
                                        var declSnippet = declEnd > m.Index ? text.Substring(m.Index, declEnd - m.Index) : string.Empty;

                                        var kindValue = EngineeringDiscovery.Core.Models.TypeKind.Class;
                                        switch (kind)
                                        {
                                            case "class": kindValue = EngineeringDiscovery.Core.Models.TypeKind.Class; break;
                                            case "interface": kindValue = EngineeringDiscovery.Core.Models.TypeKind.Interface; break;
                                            case "record": kindValue = EngineeringDiscovery.Core.Models.TypeKind.Record; break;
                                            case "struct": kindValue = EngineeringDiscovery.Core.Models.TypeKind.Struct; break;
                                            case "enum": kindValue = EngineeringDiscovery.Core.Models.TypeKind.Enum; break;
                                            case "delegate": kindValue = EngineeringDiscovery.Core.Models.TypeKind.Delegate; break;
                                        }

                                        // Compute repository-unique QualifiedName for the discovered type.
                                        // Preferred format: ProjectName:Namespace.TypeName
                                        // Fallbacks: Namespace.TypeName or FilePath:TypeName
                                        string qualifiedName;
                                        if (!string.IsNullOrWhiteSpace(name))
                                        {
                                            if (!string.IsNullOrWhiteSpace(fileNs)) qualifiedName = $"{name}:{fileNs}.{typeName}";
                                            else qualifiedName = $"{name}:{typeName}";
                                        }
                                        else if (!string.IsNullOrWhiteSpace(fileNs)) qualifiedName = $"{fileNs}.{typeName}";
                                        else qualifiedName = $"{csf}:{typeName}";

                                        var typeObs = new EngineeringDiscovery.Core.Models.TypeObservation
                                        {
                                            Project = name,
                                            Namespace = fileNs ?? string.Empty,
                                            TypeName = typeName,
                                            QualifiedName = qualifiedName,
                                            Kind = kindValue,
                                            Accessibility = string.Empty,
                                            IsAbstract = declSnippet.IndexOf("abstract", StringComparison.OrdinalIgnoreCase) >= 0,
                                            IsStatic = declSnippet.IndexOf("static", StringComparison.OrdinalIgnoreCase) >= 0,
                                            IsPartial = declSnippet.IndexOf("partial", StringComparison.OrdinalIgnoreCase) >= 0,
                                            IsGeneric = false,
                                            GenericParameterCount = 0,
                                            BaseType = null,
                                            ImplementedInterfaceCount = 0,
                                            MethodCount = 0,
                                            ConstructorCount = 0,
                                            PropertyCount = 0,
                                            FieldCount = 0,
                                            EventCount = 0,
                                            PublicMemberCount = 0,
                                            PrivateMemberCount = 0,
                                            MemberCount = 0
                                        };

                                        if (!string.IsNullOrWhiteSpace(typeBody))
                                        {
                                            try
                                            {
                                                var ctorRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+" + Regex.Escape(typeName) + "\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
                                                foreach (Match cm in ctorRegex.Matches(typeBody))
                                                {
                                                    typeObs.ConstructorCount++;
                                                    var access = cm.Groups[2].Value.Trim();
                                                    if (string.Equals(access, "public", StringComparison.OrdinalIgnoreCase)) typeObs.PublicMemberCount++;
                                                    else if (string.Equals(access, "private", StringComparison.OrdinalIgnoreCase)) typeObs.PrivateMemberCount++;
                                                }

                                                var methodRegexLocal = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|virtual\\s+|override\\s+|async\\s+|sealed\\s+|new\\s+|partial\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
                                                foreach (Match mm in methodRegexLocal.Matches(typeBody))
                                                {
                                                    typeObs.MethodCount++;
                                                    var access = mm.Groups[2].Value.Trim();
                                                    if (string.Equals(access, "public", StringComparison.OrdinalIgnoreCase)) typeObs.PublicMemberCount++;
                                                    else if (string.Equals(access, "private", StringComparison.OrdinalIgnoreCase)) typeObs.PrivateMemberCount++;
                                                }

                                                var propertyRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|virtual\\s+|override\\s+|sealed\\s+|new\\s+|partial\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\{\\s*(get\\s*;|set\\s*;|init\\s*;|get\\s*\\{|set\\s*\\{|init\\s*\\{)", RegexOptions.Compiled | RegexOptions.Multiline);
                                                foreach (Match pm in propertyRegex.Matches(typeBody))
                                                {
                                                    typeObs.PropertyCount++;
                                                    var access = pm.Groups[2].Value.Trim();
                                                    if (string.Equals(access, "public", StringComparison.OrdinalIgnoreCase)) typeObs.PublicMemberCount++;
                                                    else if (string.Equals(access, "private", StringComparison.OrdinalIgnoreCase)) typeObs.PrivateMemberCount++;
                                                }

                                                var fieldRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|readonly\\s+|const\\s+|volatile\\s+|new\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*(=|;)", RegexOptions.Compiled | RegexOptions.Multiline);
                                                foreach (Match fm in fieldRegex.Matches(typeBody))
                                                {
                                                    typeObs.FieldCount++;
                                                    var access = fm.Groups[2].Value.Trim();
                                                    if (string.Equals(access, "public", StringComparison.OrdinalIgnoreCase)) typeObs.PublicMemberCount++;
                                                    else if (string.Equals(access, "private", StringComparison.OrdinalIgnoreCase)) typeObs.PrivateMemberCount++;
                                                }

                                                var eventRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+event\\s+[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.Multiline);
                                                foreach (Match em in eventRegex.Matches(typeBody))
                                                {
                                                    typeObs.EventCount++;
                                                    var access = em.Groups[2].Value.Trim();
                                                    if (string.Equals(access, "public", StringComparison.OrdinalIgnoreCase)) typeObs.PublicMemberCount++;
                                                    else if (string.Equals(access, "private", StringComparison.OrdinalIgnoreCase)) typeObs.PrivateMemberCount++;
                                                }

                                                typeObs.MemberCount = typeObs.ConstructorCount + typeObs.MethodCount + typeObs.PropertyCount + typeObs.FieldCount + typeObs.EventCount;
                                            }
                                            catch { }
                                        }

                                        try { context.TypeObservations.Add(typeObs); } catch { }
                                        try { _investigation.AddTypeObservation(typeObs); } catch { }
                                    }
                                    catch { }

                                    // Member detection is intentionally left in InvestigationEngine for now per ED-135 scope
                                    try
                                    {
                                        var typeBody = ExtractBalancedBlock(text, m.Index);
                                        if (!string.IsNullOrWhiteSpace(typeBody))
                                        {
                                            // Constructors
                                            var ctorRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+" + Regex.Escape(typeName) + "\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
                                            foreach (Match cm in ctorRegex.Matches(typeBody))
                                            {
                                                // leave member findings to original engine
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
    }
}
