using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
            int projectCount = 0;
            var discoveredProjectNames = new List<string>();

            if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(solutionPath) ?? defaultTarget;
                    target = fileName;

                    var lines = File.ReadAllLines(solutionPath);
                    // Count Project definitions that reference a project file (simple string match)
                    projectCount = lines.Count(l => l.Contains(".csproj", StringComparison.OrdinalIgnoreCase) || l.Contains("Project("));

                    // Try to extract project names from lines that include .csproj references
                    var projectLines = lines.Where(l => l.IndexOf(".csproj", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    foreach (var pl in projectLines)
                    {
                        try
                        {
                            // Typical .sln project line: Project("{...}") = "Name", "path\to\project.csproj", "{GUID}"
                            var parts = pl.Split('=');
                            if (parts.Length < 2) continue;
                            var rhs = parts[1];
                            var namePart = rhs.Split(',').Select(p => p.Trim()).FirstOrDefault();
                            if (string.IsNullOrEmpty(namePart)) continue;
                            // Trim surrounding quotes
                            namePart = namePart.Trim();
                            if (namePart.StartsWith("\"")) namePart = namePart.Trim('"');
                            // Collect discovered project name to add later
                            discoveredProjectNames.Add(namePart);
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
                    projectCount = 0;
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
            if (projectCount > 0)
            {
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {projectCount} projects."));
            }

            // Add observations for each discovered project name
            foreach (var name in discoveredProjectNames)
            {
                try
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project discovered: {name}"));
                }
                catch
                {
                    // ignore
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
