using System;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    public class SampleDiscoveryEngine : IDiscoveryEngine
    {
        public Investigation CreateInvestigation()
        {
            var inv = Investigation.Create(
                Guid.NewGuid(),
                repositoryPath: "/",
                goal: "Assess repository for maintainability and security risks.",
                owner: "alice@example.com",
                target: "engineering-discovery",
                architectureStatus: EngineeringStageStatus.NotStarted,
                planningStatus: EngineeringStageStatus.NotStarted,
                developmentStatus: EngineeringStageStatus.NotStarted,
                verificationStatus: EngineeringStageStatus.NotStarted);

            // Seed some sample findings if desired (preserve previous behaviour)
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
