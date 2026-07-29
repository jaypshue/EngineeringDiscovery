using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    public class SolutionDiscoveryEngine : IDiscoveryEngine
    {
        private readonly string? solutionPath;

        public SolutionDiscoveryEngine(string? solutionPath = null)
        {
            this.solutionPath = solutionPath ?? FindSolutionInParents();
        }

        public Investigation CreateInvestigation(string? targetOverride = null)
        {
            // Default sample values
            var defaultGoal = "Assess repository for maintainability and security risks.";
            var defaultOwner = "alice@example.com";
            var defaultTarget = "engineering-discovery";

            string target = defaultTarget;
            var discoveredProjects = new List<(string Name, string Path)>();

            if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(solutionPath) ?? defaultTarget;
                    target = fileName;

                    var lines = File.ReadAllLines(solutionPath);

                    // Look for lines that reference project files and try to extract the project name and path
                    var projectLines = lines.Where(l => l.IndexOf(".csproj", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    var solutionDir = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
                    foreach (var pl in projectLines)
                    {
                        try
                        {
                            // Typical .sln project line: Project("{...}") = "Name", "path\to\project.csproj", "{GUID}"
                            var parts = pl.Split('=');
                            if (parts.Length < 2) continue;
                            var rhs = parts[1];
                            var segments = rhs.Split(',').Select(p => p.Trim()).ToArray();
                            if (segments.Length < 2) continue;
                            var namePart = segments[0].Trim();
                            var pathPart = segments[1].Trim();
                            if (namePart.StartsWith("\"")) namePart = namePart.Trim('"');
                            if (pathPart.StartsWith("\"")) pathPart = pathPart.Trim('"');

                            // Resolve relative project path against the solution directory
                            var projectPath = pathPart;
                            if (!Path.IsPathRooted(projectPath))
                            {
                                projectPath = Path.GetFullPath(Path.Combine(solutionDir, projectPath));
                            }

                            discoveredProjects.Add((Name: namePart, Path: projectPath));
                        }
                        catch
                        {
                            // ignore individual parse errors
                        }
                    }
                }
                catch
                {
                    // Fall back to defaults on any IO error
                    target = defaultTarget;
                    discoveredProjects.Clear();
                }
            }

            // allow the caller (UI) to override the discovered target
            if (!string.IsNullOrWhiteSpace(targetOverride)) target = targetOverride;

            var inv = Investigation.Create(
                Guid.NewGuid(),
                repositoryPath: "/",
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
            var projectCount = discoveredProjects.Count;
            if (projectCount > 0)
            {
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {projectCount} projects."));
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
                            var sourceDir = Path.GetDirectoryName(projFile) ?? Directory.GetCurrentDirectory();
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

            return inv;
        }

        private static string? FindSolutionInParents()
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
    }
}
