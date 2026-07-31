using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class TechnologyAnalysisStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Analysis;
        private readonly Investigation _inv;

        public TechnologyAnalysisStep(Investigation inv)
        {
            _inv = inv ?? throw new ArgumentNullException(nameof(inv));
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            var discoveredProjects = context.DiscoveredProjects;

            // Technology discovery: inspect each project for SDK, TargetFramework(s), and PackageReferences
            var packageIndicators = new[]
            {
                "entityframeworkcore",
                "efcore",
                "serilog",
                "automapper",
                "mediatR".ToLowerInvariant(),
                "xunit",
                "nunit",
                "mstest",
                "fluentvalidation",
                "swashbuckle",
                "signalr",
            };

            foreach (var proj in discoveredProjects)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(proj.Path)) continue;
                    var projFile = proj.Path;
                    if (!File.Exists(projFile)) continue;

                    var doc = XDocument.Load(projFile);

                    var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";

                    // Project SDK (from Project/@Sdk)
                    try
                    {
                        var sdkAttr = doc.Root?.Attribute("Sdk")?.Value;
                        if (!string.IsNullOrWhiteSpace(sdkAttr))
                        {
                            _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} uses {sdkAttr}."));
                        }
                    }
                    catch { }

                    // Target frameworks
                    try
                    {
                        var tfElems = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "TargetFramework", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(x.Name.LocalName, "TargetFrameworks", StringComparison.OrdinalIgnoreCase));
                        foreach (var tf in tfElems)
                        {
                            var tfValue = (tf?.Value ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(tfValue)) continue;

                            // TargetFrameworks can be semicolon-separated
                            var frameworks = tfValue.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                            foreach (var f in frameworks)
                            {
                                _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project {name} targets {f}."));
                            }
                        }
                    }
                    catch { }

                    // PackageReferences: report every referenced package and collect for capability inference and analyzer detection
                    List<string> discoveredPackages = new List<string>();
                    try
                    {
                        var packageRefs = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase));
                        foreach (var pr in packageRefs)
                        {
                            var include = pr.Attribute("Include")?.Value ?? pr.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Include", StringComparison.OrdinalIgnoreCase))?.Value;
                            if (string.IsNullOrWhiteSpace(include)) continue;

                            discoveredPackages.Add(include);
                            _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references package '{include}'."));
                        }
                    }
                    catch { }

                    // Analyzer and framework inference
                    try
                    {
                        // analyzers
                        var analyzerRefs = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "Analyzer", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase) && (x.Attribute("Include")?.Value ?? string.Empty).IndexOf("analy", StringComparison.OrdinalIgnoreCase) >= 0);
                        foreach (var a in analyzerRefs)
                        {
                            var inc = a.Attribute("Include")?.Value ?? a.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Include", StringComparison.OrdinalIgnoreCase))?.Value;
                            if (string.IsNullOrWhiteSpace(inc)) continue;
                            _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references analyzer '{inc}'."));
                        }
                    }
                    catch { }

                    // AssemblyName, RootNamespace, ProjectFolder
                    try
                    {
                        var assemblyName = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "AssemblyName", StringComparison.OrdinalIgnoreCase))?.Value;
                        var rootNamespace = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "RootNamespace", StringComparison.OrdinalIgnoreCase))?.Value;

                        var projectFolder = Path.GetDirectoryName(proj.Path) ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(projectFolder))
                        {
                            _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' is located in folder '{projectFolder}'."));
                        }

                        if (!string.IsNullOrWhiteSpace(assemblyName))
                        {
                            _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' has assembly name '{assemblyName}'."));
                        }

                        if (!string.IsNullOrWhiteSpace(rootNamespace))
                        {
                            _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' has root namespace '{rootNamespace}'."));
                        }

                        // Namespace discovery will be handled by NamespaceDiscoveryStep
                    }
                    catch { }
                }
                catch
                {
                    // ignore per-project tech discovery errors
                }
            }
        }
    }
}
