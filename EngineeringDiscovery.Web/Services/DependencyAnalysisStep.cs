using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class DependencyAnalysisStep : IInvestigationStep
    {
        private readonly Investigation _inv;

        public DependencyAnalysisStep(Investigation inv)
        {
            _inv = inv ?? throw new ArgumentNullException(nameof(inv));
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            var discoveredProjects = context.DiscoveredProjects;

            try
            {
                // Map project full path -> name for quick lookup
                var pathToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in discoveredProjects)
                {
                    try
                    {
                        var full = Path.GetFullPath(p.Path ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(full) && !pathToName.ContainsKey(full)) pathToName[full] = p.Name ?? Path.GetFileNameWithoutExtension(p.Path) ?? full;
                    }
                    catch { }
                }

                // Build adjacency list (source -> list of referenced project names)
                var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in discoveredProjects)
                {
                    var sourceName = p.Name ?? Path.GetFileNameWithoutExtension(p.Path) ?? "Unnamed";
                    if (!adjacency.ContainsKey(sourceName)) adjacency[sourceName] = new List<string>();
                    try
                    {
                        if (string.IsNullOrWhiteSpace(p.Path) || !File.Exists(p.Path)) continue;
                        var doc = XDocument.Load(p.Path);
                        var projectReferences = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase));
                        var sourceDir = Path.GetDirectoryName(p.Path) ?? Directory.GetCurrentDirectory();
                        foreach (var pr in projectReferences)
                        {
                            try
                            {
                                var includeAttr = pr.Attribute("Include")?.Value;
                                if (string.IsNullOrWhiteSpace(includeAttr)) continue;
                                var referencedPath = includeAttr;
                                if (!Path.IsPathRooted(referencedPath)) referencedPath = Path.GetFullPath(Path.Combine(sourceDir, referencedPath));
                                else referencedPath = Path.GetFullPath(referencedPath);

                                if (pathToName.TryGetValue(referencedPath, out var rn))
                                {
                                    adjacency[sourceName].Add(rn);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // Optionally emit adjacency summary
                foreach (var kv in adjacency)
                {
                    if (kv.Value.Count > 0)
                    {
                        var rel = $"Project '{kv.Key}' depends on {string.Join(", ", kv.Value.Distinct())}.";
                        _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, rel));
                    }
                }
            }
            catch { }
        }
    }
}
