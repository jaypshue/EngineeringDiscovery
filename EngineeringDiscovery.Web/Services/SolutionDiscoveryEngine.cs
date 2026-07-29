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

        public Investigation CreateInvestigation()
        {
            // Default sample values
            var defaultGoal = "Assess repository for maintainability and security risks.";
            var defaultOwner = "alice@example.com";
            var defaultTarget = "engineering-discovery";

            string target = defaultTarget;
            int projectCount = 0;

            if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(solutionPath) ?? defaultTarget;
                    target = fileName;

                    var lines = File.ReadAllLines(solutionPath);
                    // Count Project definitions that reference a project file (simple string match)
                    projectCount = lines.Count(l => l.Contains(".csproj", StringComparison.OrdinalIgnoreCase) || l.Contains("Project("));
                }
                catch
                {
                    // Fall back to defaults on any IO error
                    target = defaultTarget;
                    projectCount = 0;
                }
            }

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
