using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class MemberDiscoveryStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Discovery;
        private readonly Investigation _investigation;

        public MemberDiscoveryStep(Investigation investigation)
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
                    if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder)) continue;

                    var csFilesAll = Directory.GetFiles(projectFolder, "*.cs", SearchOption.AllDirectories);

                    foreach (var csf in csFilesAll)
                    {
                        try
                        {
                            var text = File.ReadAllText(csf);
                            var typeRegex = new Regex("\\b(class|interface|record(?:\\s+class|\\s+struct)?|struct|enum|delegate)\\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
                            var nsRegexPerFile = new Regex("\\bnamespace\\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);
                            var nsMatch = nsRegexPerFile.Match(text);
                            var fileNs = nsMatch.Success ? nsMatch.Groups[1].Value.Trim() : string.Empty;

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

                            foreach (Match m in typeRegex.Matches(text))
                            {
                                try
                                {
                                    var typeName = m.Groups[2].Value.Trim();
                                    var typeBody = ExtractBalancedBlock(text, m.Index);
                                    if (string.IsNullOrWhiteSpace(typeBody)) continue;

                                    // Constructors
                                    var ctorRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+" + Regex.Escape(typeName) + "\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
                                    foreach (Match cm in ctorRegex.Matches(typeBody))
                                    {
                                        var desc = $"Project '{name}' defines constructor '{typeName}' in type '{typeName}'.";
                                        _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, desc));
                                        _investigation.AddObservation(new DiscoveryObservation
                                        {
                                            Kind = ObservationKind.Member,
                                            Project = name,
                                            Type = typeName,
                                            Member = typeName,
                                            Description = desc
                                        });
                                    }

                                    // Methods
                                    var methodRegexLocal = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|virtual\\s+|override\\s+|async\\s+|sealed\\s+|new\\s+|partial\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
                                    foreach (Match mm in methodRegexLocal.Matches(typeBody))
                                    {
                                        var methodName = mm.Groups[4].Value.Trim();
                                        if (string.IsNullOrWhiteSpace(methodName)) continue;
                                        if (string.Equals(methodName, typeName, StringComparison.OrdinalIgnoreCase)) continue;
                                        var desc = $"Project '{name}' defines method '{methodName}' in type '{typeName}'.";
                                        _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, desc));
                                        _investigation.AddObservation(new DiscoveryObservation
                                        {
                                            Kind = ObservationKind.Member,
                                            Project = name,
                                            Type = typeName,
                                            Member = methodName,
                                            Description = desc
                                        });
                                    }

                                    // Properties
                                    var propertyRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|virtual\\s+|override\\s+|sealed\\s+|new\\s+|partial\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\{\\s*(get\\s*;|set\\s*;|init\\s*;|get\\s*\\{|set\\s*\\{|init\\s*\\{)", RegexOptions.Compiled | RegexOptions.Multiline);
                                    foreach (Match pm in propertyRegex.Matches(typeBody))
                                    {
                                        var propertyName = pm.Groups[4].Value.Trim();
                                        if (string.IsNullOrWhiteSpace(propertyName)) continue;
                                        var desc = $"Project '{name}' defines property '{propertyName}' in type '{typeName}'.";
                                        _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, desc));
                                        _investigation.AddObservation(new DiscoveryObservation
                                        {
                                            Kind = ObservationKind.Member,
                                            Project = name,
                                            Type = typeName,
                                            Member = propertyName,
                                            Description = desc
                                        });
                                    }

                                    // Fields
                                    var fieldRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|readonly\\s+|const\\s+|volatile\\s+|new\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*(=|;)", RegexOptions.Compiled | RegexOptions.Multiline);
                                    foreach (Match fm in fieldRegex.Matches(typeBody))
                                    {
                                        var fieldName = fm.Groups[4].Value.Trim();
                                        if (string.IsNullOrWhiteSpace(fieldName)) continue;
                                        var desc = $"Project '{name}' defines field '{fieldName}' in type '{typeName}'.";
                                        _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, desc));
                                        _investigation.AddObservation(new DiscoveryObservation
                                        {
                                            Kind = ObservationKind.Member,
                                            Project = name,
                                            Type = typeName,
                                            Member = fieldName,
                                            Description = desc
                                        });
                                    }

                                    // Events
                                    var eventRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+event\\s+[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.Multiline);
                                    foreach (Match em in eventRegex.Matches(typeBody))
                                    {
                                        var eventName = em.Groups[3].Value.Trim();
                                        if (string.IsNullOrWhiteSpace(eventName)) continue;
                                        var desc = $"Project '{name}' defines event '{eventName}' in type '{typeName}'.";
                                        _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, desc));
                                        _investigation.AddObservation(new DiscoveryObservation
                                        {
                                            Kind = ObservationKind.Member,
                                            Project = name,
                                            Type = typeName,
                                            Member = eventName,
                                            Description = desc
                                        });
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
    }
}
