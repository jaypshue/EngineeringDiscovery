using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Models;
using System.Text.RegularExpressions;
using EngineeringDiscovery.Core.Domain.Investigation;
using System.Diagnostics;

namespace EngineeringDiscovery.Web.Services
{
    public class InvestigationEngine : IInvestigationEngine
    {
        public InvestigationEngine()
        {
        }

        public Investigation CreateInvestigation(string? repositoryRoot = null, string? targetOverride = null)
        {
            // Default sample values
            var defaultGoal = "Assess repository for maintainability and security risks.";
            var defaultOwner = "alice@example.com";
            var defaultTarget = "engineering-discovery";

            string target = defaultTarget;
            var discoveredProjects = new List<(string Name, string Path)>();

            // Create discovery context to share state and diagnostics
            var context = new InvestigationContext(null);
            // repositoryRoot may be provided by the UI; the engine will search for solutions under it when needed
            var effectiveRepositoryRoot = repositoryRoot;

            // Determine effective solution path based on repositoryRoot
            string? effectiveSolutionPath = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(effectiveRepositoryRoot))
                {
                    // If the provided repositoryRoot is actually a solution file, use it directly
                    if (File.Exists(effectiveRepositoryRoot) && (effectiveRepositoryRoot.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || effectiveRepositoryRoot.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
                    {
                        effectiveSolutionPath = effectiveRepositoryRoot;
                    }
                    else if (Directory.Exists(effectiveRepositoryRoot))
                    {
                        var sols = FindSolutionsInRepository(effectiveRepositoryRoot);
                        if (sols.Length == 1)
                        {
                            effectiveSolutionPath = sols[0];
                        }
                        else if (sols.Length == 0)
                        {
                            context.AddDiagnostic($"No solution files (*.sln, *.slnx) found under repository root: {effectiveRepositoryRoot}");
                        }
                        else
                        {
                            // Multiple solutions found - choose the first but note the ambiguity
                            context.AddDiagnostic($"Multiple solutions found under repository root: {effectiveRepositoryRoot}. Using first: {Path.GetFileName(sols[0])}");
                            effectiveSolutionPath = sols[0];
                        }
                    }
                }
                else
                {
                    // No repositoryRoot provided - intentionally do not fall back to execution directory.
                    // Discovery requires an explicit repositoryRoot from the UI. Record a diagnostic.
                    context.AddDiagnostic("No repository root provided. Discovery requires an explicit repository root from the UI.");
                }
            }
            catch (Exception ex)
            {
                context.AddDiagnostic($"Failed locating solution: {ex.Message}");
            }

            // Solution-wide type totals
            var totalClasses = 0;
            var totalInterfaces = 0;
            var totalRecords = 0;
            var totalStructs = 0;
            var totalEnums = 0;
            var totalDelegates = 0;
            var totalConstructors = 0;
            var totalMethods = 0;
            var totalProperties = 0;
            var totalFields = 0;
            var totalEvents = 0;

            // Delegate project discovery to the pipeline step
            if (!string.IsNullOrEmpty(effectiveSolutionPath) && File.Exists(effectiveSolutionPath))
            {
                try
                {
                    // set solution path on context so steps can access it
                    context = new InvestigationContext(effectiveSolutionPath);

                    // run project discovery
                    var projectStep = new ProjectDiscoveryStep();
                    projectStep.Execute(context);

                    // namespace discovery will be invoked after Investigation is created

                    // copy discovered projects into local list for backward compatibility
                    discoveredProjects.AddRange(context.DiscoveredProjects);

                    // determine target from solution filename as before
                    var fileName = Path.GetFileNameWithoutExtension(effectiveSolutionPath) ?? defaultTarget;
                    target = fileName;
                }
                catch (Exception ex)
                {
                    // Fall back to defaults on any IO error
                    target = defaultTarget;
                    discoveredProjects.Clear();
                    context.AddDiagnostic($"Failed during project discovery: {ex.Message}");
                }
            }

            // allow the caller (UI) to override the discovered target
            if (!string.IsNullOrWhiteSpace(targetOverride)) target = targetOverride;

            // Solution directory (if available) for solution-level discovery
            var solutionDirLocal = !string.IsNullOrWhiteSpace(effectiveSolutionPath) ? Path.GetDirectoryName(effectiveSolutionPath) : null;

            var inv = Investigation.Create(
                Guid.NewGuid(),
                repositoryPath: effectiveRepositoryRoot ?? Path.GetDirectoryName(effectiveSolutionPath) ?? "/",
                goal: defaultGoal,
                owner: defaultOwner,
                target: target,
                architectureStatus: EngineeringStageStatus.NotStarted,
                planningStatus: EngineeringStageStatus.NotStarted,
                developmentStatus: EngineeringStageStatus.NotStarted,
                verificationStatus: EngineeringStageStatus.NotStarted);

            // Preserve previous behavior: start investigation and seed sample findings
            inv.Start();
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Architecture, "API follows layered architecture."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Risk, "Authentication library is deprecated."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Decision, "WorkspaceHost owns the Investigation aggregate."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Question, "Which authentication provider should we adopt?"));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.TechnicalDebt, "Legacy authentication module requires refactoring."));

            // Add observation about solution/project count when discovered
            var projectCount = context.DiscoveredProjects.Count;
            if (projectCount > 0)
            {
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {projectCount} projects."));
                inv.AddObservation(new DiscoveryObservation
                {
                    Kind = ObservationKind.Solution,
                    Project = string.Empty,
                    Description = $"Solution contains {projectCount} projects."
                });
            }

            // Infer project types using simple filename/name conventions and add observations
            foreach (var proj in discoveredProjects)
            {
                try
                {
                    var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                    var lowered = name.ToLowerInvariant();
                    string projType;

                    if (lowered.Contains("test") || lowered.Contains("tests")) projType = "Test Project";
                    else if (lowered.Contains("web") || lowered.Contains("api")) projType = "Web";
                    else if (lowered.Contains("console") || lowered.Contains("app")) projType = "Console";
                    else if (lowered.Contains("core") || lowered.Contains("lib") || lowered.Contains("common") || lowered.Contains("shared")) projType = "Class Library";
                    else projType = "Unknown";

                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} (Project Type: {projType})"));
                    inv.AddObservation(new DiscoveryObservation
                    {
                        Kind = ObservationKind.Project,
                        Project = name,
                        Description = $"{name} (Project Type: {projType})"
                    });
                }
                catch
                {
                    // ignore per-project errors
                }
            }

            // Project reference discovery: inspect each discovered .csproj for ProjectReference elements
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
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{sourceName} references {referencedName}."));
                                inv.AddObservation(new DiscoveryObservation
                                {
                                    Kind = ObservationKind.Dependency,
                                    Project = sourceName,
                                    Description = $"{sourceName} references {referencedName}.",
                                    // referencedName is available in description; Namespace/Type/Member not applicable
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

            // Move technology interpretation into TechnologyAnalysisStep to separate Analysis from Discovery.
            try
            {
                var techStep = new TechnologyAnalysisStep(inv);
                techStep.Execute(context);
            }
            catch { }

            // Architectural layer inference: infer a layer for each discovered project and report relationships
            try
            {
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

                // Map project name -> layer
                var projectLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var proj in discoveredProjects)
                {
                    try
                    {
                        var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                        var layer = InferLayer(name, proj.Path);
                        projectLayer[name] = layer;
                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} appears to be the {layer}."));
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
                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, rel));
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

            // Dependency graph discovery: build project dependency graph using ProjectReference elements
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
                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, rel));
                    }
                }
            }
            catch { }

            return inv;
        }

        // Note: Do not use AppContext.BaseDirectory or Directory.GetCurrentDirectory() to find repository.
        // This helper remains for legacy upward search but is no longer used by CreateInvestigation when repositoryRoot is required.
        public string? FindSolutionAboveExecutionDirectory()
        {
            try
            {
                var dir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var info = new DirectoryInfo(dir);
                while (info != null)
                {
                    var sln = info.GetFiles("*.sln*").FirstOrDefault();
                    if (sln != null) return sln.FullName;
                    info = info.Parent;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }

        // Find solutions under a repository root honoring exclusions
        public string[] FindSolutionsInRepository(string repositoryRoot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot)) return Array.Empty<string>();
                var ctx = new InvestigationContext(null);
                var files = Directory.GetFiles(repositoryRoot, "*.sln*", SearchOption.AllDirectories)
                    .Where(f => !ctx.IsExcludedPath(f)).ToArray();
                return files;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
