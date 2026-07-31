using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class LayerAnalysisStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Analysis;
        private readonly Investigation _inv;

        public LayerAnalysisStep(Investigation inv)
        {
            _inv = inv ?? throw new ArgumentNullException(nameof(inv));
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            // Helper to infer a layer string from project name and project file SDK
            string InferLayer(string projName, string projFile)
            {
                var lowered = (projName ?? string.Empty).ToLowerInvariant();
                try
                {
                    if (!string.IsNullOrWhiteSpace(projFile) && File.Exists(projFile))
                    {
                        var doc = XDocument.Load(projFile);
                        var sdkAttr = doc.Root?.Attribute("Sdk")?.Value ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(sdkAttr) && sdkAttr.IndexOf("microsoft.net.sdk.web", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return "Web layer";
                        }
                    }
                }
                catch { }

                if (lowered.EndsWith(".api") || lowered.Contains(".api")) return "API layer";
                if (lowered.Contains("web")) return "Web layer";
                if (lowered.Contains("core")) return "Core domain layer";
                if (lowered.Contains("infrastructure")) return "Infrastructure layer";
                if (lowered.Contains("tests") || lowered.Contains("test")) return "Test layer";
                if (lowered.Contains("shared")) return "Shared layer";
                return "Unknown layer";
            }

            var discoveredProjects = context.DiscoveredProjects;

            try
            {
                // Map project name -> layer
                var projectLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var proj in discoveredProjects)
                {
                    try
                    {
                        var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                        var layer = InferLayer(name, proj.Path);
                        projectLayer[name] = layer;
                        _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} appears to be the {layer}."));
                    }
                    catch { }
                }

                // Now inspect references again to produce layer-to-layer relationship findings
                var relationshipSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var proj in discoveredProjects)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(proj.Path)) continue;
                        var projFile = proj.Path;
                        if (!File.Exists(projFile)) continue;

                        var doc = XDocument.Load(projFile);
                        var projectReferences = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase));
                        var sourceName = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                        var sourceLayer = projectLayer.ContainsKey(sourceName) ? projectLayer[sourceName] : "Unknown layer";

                        foreach (var pr in projectReferences)
                        {
                            try
                            {
                                var includeAttr = pr.Attribute("Include")?.Value;
                                if (string.IsNullOrWhiteSpace(includeAttr)) continue;
                                var sourceDir = Path.GetDirectoryName(projFile) ?? throw new InvalidOperationException("Project file path must be rooted or within a discovered solution directory.");
                                var referencedPath = includeAttr;
                                if (!Path.IsPathRooted(referencedPath)) referencedPath = Path.GetFullPath(Path.Combine(sourceDir, referencedPath));
                                else referencedPath = Path.GetFullPath(referencedPath);

                                var referencedProject = discoveredProjects.FirstOrDefault(d =>
                                    !string.IsNullOrEmpty(d.Path) &&
                                    string.Equals(Path.GetFullPath(d.Path), referencedPath, StringComparison.OrdinalIgnoreCase));

                                var referencedName = referencedProject.Name ?? Path.GetFileNameWithoutExtension(referencedPath) ?? "Unnamed";
                                var referencedLayer = projectLayer.ContainsKey(referencedName) ? projectLayer[referencedName] : "Unknown layer";

                                if (!string.Equals(sourceLayer, referencedLayer, StringComparison.OrdinalIgnoreCase))
                                {
                                    var rel = $"{sourceLayer} depends on {referencedLayer}.";
                                    if (!relationshipSet.Contains(rel))
                                    {
                                        relationshipSet.Add(rel);
                                        _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, rel));

                                        // ED-148: produce first engineering artifact when Presentation/Web layer depends on Infrastructure
                                        try
                                        {
                                            if ((sourceLayer.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0 || sourceLayer.IndexOf("presentation", StringComparison.OrdinalIgnoreCase) >= 0)
                                                && referencedLayer.IndexOf("infrastructure", StringComparison.OrdinalIgnoreCase) >= 0)
                                            {
                                                var title = "Presentation layer depends on Infrastructure";
                                                var description = rel;
                                                _inv.Artifacts.Add(new InvestigationArtifact(Guid.NewGuid(), title, description));
                                            }
                                        }
                                        catch { }
                                    }
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
