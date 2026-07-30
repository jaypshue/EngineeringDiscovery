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
