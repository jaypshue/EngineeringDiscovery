using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class ProjectClassificationStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Discovery;
        private readonly Investigation _inv;

        public ProjectClassificationStep(Investigation inv)
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
                    var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                    var lowered = name.ToLowerInvariant();
                    string projType;

                    if (lowered.Contains("test") || lowered.Contains("tests")) projType = "Test Project";
                    else if (lowered.Contains("web") || lowered.Contains("api")) projType = "Web";
                    else if (lowered.Contains("console") || lowered.Contains("app")) projType = "Console";
                    else if (lowered.Contains("core") || lowered.Contains("lib") || lowered.Contains("common") || lowered.Contains("shared")) projType = "Class Library";
                    else projType = "Unknown";

                    _inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} (Project Type: {projType})"));
                    _inv.AddObservation(new DiscoveryObservation
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
        }
    }
}
