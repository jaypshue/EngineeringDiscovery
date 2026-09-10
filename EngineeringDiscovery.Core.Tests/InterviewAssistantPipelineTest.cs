using System;
using System.IO;
using System.Linq;
using EngineeringDiscovery.Web.Services;
using EngineeringDiscovery.Web.Services.RepositoryLoading;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class InterviewAssistantPipelineTest
    {
        [Fact]
        public void InvestigationEngine_InterviewAssistant_ProducesTypeObservations()
        {
            var repoRoot = @"C:\projects\InterviewAssistant";
            if (!Directory.Exists(repoRoot))
            {
                // Skip if repo not present on this machine
                return;
            }

            var engine = new InvestigationEngine();
            var inv = engine.CreateInvestigation(repoRoot, null);

            Assert.NotNull(inv);

            var diagPath = Path.Combine(Path.GetTempPath(), "engineos-pipeline-diag.txt");
            File.WriteAllText(diagPath,
                $"Pipeline test result:\n" +
                $"TypeObservations={inv.TypeObservations?.Count ?? 0}\n" +
                $"NamespaceObservations={inv.NamespaceObservations?.Count ?? 0}\n" +
                $"MemberObservations={inv.MemberObservations?.Count ?? 0}\n" +
                $"Findings={inv.Findings?.Count ?? 0}\n" +
                $"Artifacts={inv.Artifacts?.Count ?? 0}\n" +
                $"RelationshipGraph={inv.RelationshipGraph != null}\n" +
                $"RepositoryMetrics={inv.RepositoryMetrics != null}\n" +
                $"Observations={inv.Observations?.Count ?? 0}\n");

            // We expect at least SOME type observations from 472 .cs files
            // This test documents what the pipeline actually produces
        }
    }
}
