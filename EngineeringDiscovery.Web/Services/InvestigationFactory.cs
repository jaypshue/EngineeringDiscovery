using System;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Domain;

namespace EngineeringDiscovery.Web.Services
{
    internal static class InvestigationFactory
    {
        public static Investigation Create(string? repositoryPath, string target, string? goal = null, string? owner = null)
        {
            var defaultGoal = goal ?? "Assess repository for maintainability and security risks.";
            var defaultOwner = owner ?? "alice@example.com";

            var inv = Investigation.Create(
                Guid.NewGuid(),
                repositoryPath: repositoryPath ?? "/",
                goal: defaultGoal,
                owner: defaultOwner,
                target: target,
                architectureStatus: EngineeringStageStatus.NotStarted,
                planningStatus: EngineeringStageStatus.NotStarted,
                developmentStatus: EngineeringStageStatus.NotStarted,
                verificationStatus: EngineeringStageStatus.NotStarted);

            // Seed some sample findings to preserve previous behavior
            inv.Start();
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Architecture, "API follows layered architecture."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Risk, "Authentication library is deprecated."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Decision, "WorkspaceHost owns the Investigation aggregate."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Question, "Which authentication provider should we adopt?"));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.TechnicalDebt, "Legacy authentication module requires refactoring."));

            return inv;
        }
    }
}
