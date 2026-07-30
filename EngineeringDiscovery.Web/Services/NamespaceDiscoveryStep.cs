using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Domain.Models;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Domain.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class NamespaceDiscoveryStep : IInvestigationStep
    {
        private readonly EngineeringDiscovery.Core.Domain.Investigation.Investigation? _investigation;

        public NamespaceDiscoveryStep(EngineeringDiscovery.Core.Domain.Investigation.Investigation? investigation)
        {
            _investigation = investigation;
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;
            if (_investigation == null) return;

            foreach (var proj in context.DiscoveredProjects)
            {
                try
                {
                    var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                    if (string.IsNullOrWhiteSpace(proj.Path) || !File.Exists(proj.Path)) continue;

                    var doc = XDocument.Load(proj.Path);
                    var projectFolder = Path.GetDirectoryName(proj.Path) ?? string.Empty;
                    var rootNamespace = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "RootNamespace", StringComparison.OrdinalIgnoreCase))?.Value;

                    var namespaces = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(projectFolder) && Directory.Exists(projectFolder))
                    {
                        var csFiles = Directory.GetFiles(projectFolder, "*.cs", SearchOption.AllDirectories);
                        var nsRegex = new Regex("\\bnamespace\\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);
                        foreach (var csf in csFiles)
                        {
                            try
                            {
                                var text = File.ReadAllText(csf);
                                var matches = nsRegex.Matches(text);
                                foreach (Match m in matches)
                                {
                                    var ns = m.Groups[1].Value?.Trim();
                                    if (!string.IsNullOrWhiteSpace(ns)) namespaces.Add(ns);
                                }
                            }
                            catch { }
                        }
                    }

                    if (namespaces.Count > 0)
                    {
                        foreach (var ns in namespaces.OrderBy(x => x))
                        {
                            var desc = $"Project '{name}' defines namespace '{ns}'.";
                            _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, desc));
                            _investigation.AddObservation(new DiscoveryObservation
                            {
                                Kind = ObservationKind.Namespace,
                                Project = name,
                                Namespace = ns,
                                Description = desc
                            });
                        }

                        var countDesc = $"Project '{name}' contains {namespaces.Count} namespaces.";
                        _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, countDesc));
                        _investigation.AddObservation(new DiscoveryObservation
                        {
                            Kind = ObservationKind.Namespace,
                            Project = name,
                            Description = countDesc
                        });

                        // Determine root namespace: prefer RootNamespace from csproj when present, otherwise pick the least-nested namespace
                        var rootNs = rootNamespace;
                        if (string.IsNullOrWhiteSpace(rootNs))
                        {
                            rootNs = namespaces.OrderBy(s => s.Count(c => c == '.')).FirstOrDefault();
                        }

                        if (!string.IsNullOrWhiteSpace(rootNs))
                        {
                            var rootDesc = $"Project '{name}' root namespace is '{rootNs}'.";
                            _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, rootDesc));
                            _investigation.AddObservation(new DiscoveryObservation
                            {
                                Kind = ObservationKind.Namespace,
                                Project = name,
                                Namespace = rootNs,
                                Description = rootDesc
                            });
                        }

                        var nestedCount = namespaces.Count(s => s.Contains('.'));
                        if (nestedCount > 0)
                        {
                            var nestedDesc = $"Project '{name}' contains {nestedCount} nested namespaces.";
                            _investigation.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, nestedDesc));
                            _investigation.AddObservation(new DiscoveryObservation
                            {
                                Kind = ObservationKind.Namespace,
                                Project = name,
                                Description = nestedDesc
                            });
                        }
                    }
                }
                catch { }
            }
        }
    }
}
