using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class ProjectReferenceDiscoveryStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Discovery;
        private readonly Investigation _inv;

        public ProjectReferenceDiscoveryStep(Investigation inv)
        {
            _inv = inv ?? throw new ArgumentNullException(nameof(inv));
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            var discoveredProjects = context.DiscoveredProjects;

            foreach (var proj in discoveredProjects)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(proj.Path)) continue;
                    var projFile = proj.Path;
                    if (!File.Exists(projFile)) continue;

                    var doc = XDocument.Load(projFile);
                    // Find ProjectReference elements in the XML namespace-agnostic way
                    var projectReferences = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase));
                    foreach (var pr in projectReferences)
                    {
                        try
                        {
                            var includeAttr = pr.Attribute("Include")?.Value;
                            if (string.IsNullOrWhiteSpace(includeAttr)) continue;

                            // Resolve referenced project path relative to the source project's directory
                            var sourceDir = Path.GetDirectoryName(projFile) ?? throw new InvalidOperationException("Project file path must be rooted or within a discovered solution directory.");
                            var referencedPath = includeAttr;
                            if (!Path.IsPathRooted(referencedPath)) referencedPath = Path.GetFullPath(Path.Combine(sourceDir, referencedPath));
                            else referencedPath = Path.GetFullPath(referencedPath);

                            // Try to find a discovered project that matches the referenced path
                            var referencedProject = discoveredProjects.FirstOrDefault(d =>
                                !string.IsNullOrEmpty(d.Path) &&
                                string.Equals(Path.GetFullPath(d.Path), referencedPath, StringComparison.OrdinalIgnoreCase));

                            var sourceName = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                            var referencedName = referencedProject.Name ?? Path.GetFileNameWithoutExtension(referencedPath) ?? "Unnamed";

                            // Only add a finding if the referenced project is different from the source
                            if (!string.IsNullOrEmpty(referencedName) && !string.Equals(sourceName, referencedName, StringComparison.OrdinalIgnoreCase))
                            {
                                _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{sourceName} references {referencedName}."));
                                _inv.AddObservation(new DiscoveryObservation
                                {
                                    Kind = ObservationKind.Dependency,
                                    Project = sourceName,
                                    Description = $"{sourceName} references {referencedName}.",
                                });
                            }
                        }
                        catch
                        {
                            // ignore individual project reference parse errors
                        }
                    }
                }
                catch
                {
                    // ignore per-project inspection errors
                }
            }
        }
    }
}
