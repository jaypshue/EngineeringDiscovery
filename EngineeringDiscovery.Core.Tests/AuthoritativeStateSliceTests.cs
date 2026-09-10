using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Observations;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Tests.Tests;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public sealed class AuthoritativeStateSliceTests
    {
        [Fact]
        public void QueryReturnsOnlyOpenIssuesAndOrdersRecentEngagements()
        {
            var state = new WorkspaceState(new InMemoryWorkspacePersistence(), new TestFingerprintService());
            var service = new ProjectStateService(state);
            var older = new WorkerEngagement { Id = Guid.NewGuid(), StartedUtc = DateTime.UtcNow.AddMinutes(-10), TaskDescription = "older" };
            var newer = new WorkerEngagement { Id = Guid.NewGuid(), StartedUtc = DateTime.UtcNow, TaskDescription = "newer" };
            service.RecordWorkerEngagement(older);
            service.RecordWorkerEngagement(newer);
            var open = new KnownIssue { Id = Guid.NewGuid(), Title = "open", Status = IssueStatus.Open };
            var resolved = new KnownIssue { Id = Guid.NewGuid(), Title = "resolved", Status = IssueStatus.Resolved };
            service.RecordKnownIssue(open);
            service.RecordKnownIssue(resolved);
            var query = new EngineeringStateQuery(state);

            var engagements = query.GetRecentEngagements(2);
            var issues = query.GetOpenIssues();

            Assert.Equal(newer.Id, engagements[0].Id);
            Assert.Equal(older.Id, engagements[1].Id);
            Assert.Single(issues);
            Assert.Equal(open.Id, issues[0].Id);
            Assert.Contains("Open issues: 1", query.GenerateStatusSummary());
        }

        [Fact]
        public async Task ObservationEngineRoutesAndDeduplicatesProjectStateObservations()
        {
            var state = new WorkspaceState(new InMemoryWorkspacePersistence(), new TestFingerprintService());
            var service = new ProjectStateService(state);
            var engine = new ObservationEngine(new InMemoryEngineeringModelRepository(), service);
            var capabilityObservation = new Observation
            {
                Id = Guid.NewGuid(),
                Type = "CapabilityVerified",
                Source = "Test",
                PayloadJson = JsonSerializer.Serialize(new { title = "Verified capability", description = "It works" })
            };
            var issueObservation = new Observation
            {
                Id = Guid.NewGuid(),
                Type = "IssueReported",
                Source = "Test",
                PayloadJson = JsonSerializer.Serialize(new { title = "Open issue", severity = "High" })
            };
            var engagementId = Guid.NewGuid();
            var workerObservation = new Observation
            {
                Id = engagementId,
                Type = "WorkerStarted",
                Source = "Test",
                PayloadJson = JsonSerializer.Serialize(new { id = engagementId, workerId = Guid.NewGuid(), taskDescription = "Do work" })
            };

            await engine.IngestAsync(capabilityObservation);
            await engine.IngestAsync(capabilityObservation);
            await engine.IngestAsync(issueObservation);
            await engine.IngestAsync(issueObservation);
            await engine.IngestAsync(workerObservation);
            await engine.IngestAsync(workerObservation);
            await engine.IngestAsync(new Observation
            {
                Type = "WorkerCompleted",
                Source = "Test",
                PayloadJson = JsonSerializer.Serialize(new { engagementId, outcome = "Completed", acceptance = "Accepted" })
            });
            await engine.IngestAsync(new Observation
            {
                Type = "ProjectIdentityUpdated",
                Source = "Test",
                PayloadJson = JsonSerializer.Serialize(new { name = "ObservedProject", description = "Observed" })
            });
            await engine.IngestAsync(new Observation
            {
                Type = "IssueResolved",
                Source = "Test",
                PayloadJson = JsonSerializer.Serialize(new { issueId = issueObservation.Id, resolution = "fixed" })
            });

            var current = service.GetCurrentState();
            Assert.NotNull(current);
            Assert.Single(current!.CompletedCapabilities);
            Assert.Equal(CapabilityAcceptance.Accepted, current.CompletedCapabilities[0].Acceptance);
            Assert.Single(current.KnownIssues);
            Assert.Equal(IssueStatus.Resolved, current.KnownIssues[0].Status);
            Assert.Single(current.WorkerEngagements);
            Assert.Equal(EngagementOutcome.Completed, current.WorkerEngagements[0].Outcome);
            Assert.Equal(AcceptanceStatus.Accepted, current.WorkerEngagements[0].Acceptance);
            Assert.Equal("ObservedProject", current.Identity!.Name);
        }

        [Fact]
        public async Task WorkerEngagementOutcomeAndHandoffSurviveFileReload()
        {
            var folder = Path.Combine(Path.GetTempPath(), "EngineOS-state-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new FileWorkspacePersistence(folder);
                var state = new WorkspaceState(persistence, new TestFingerprintService());
                var service = new ProjectStateService(state);
                service.SetProjectIdentity(new ProjectIdentity { Name = "PersistedProject" });
                service.UpdateLifecyclePhase(LifecyclePhase.ActiveDevelopment, "State slice");
                var engagement = new WorkerEngagement { Id = Guid.NewGuid(), WorkerName = "Worker", TaskDescription = "Persist work" };
                service.RecordWorkerEngagement(engagement);
                service.UpdateEngagementOutcome(engagement.Id, EngagementOutcome.Completed, AcceptanceStatus.Accepted);
                service.AssembleHandoff(new HandoffState { Objective = "Continue verification" });

                var reloadedState = new WorkspaceState(persistence, new TestFingerprintService());
                var loaded = await persistence.LoadAsync();
                Assert.NotNull(loaded);
                reloadedState.ReplaceWorkspace(loaded!);
                var query = new EngineeringStateQuery(reloadedState);

                Assert.Equal("PersistedProject", query.GetProjectIdentity()!.Name);
                Assert.Equal(LifecyclePhase.ActiveDevelopment, query.GetLifecycle()!.Phase);
                Assert.Single(query.GetRecentEngagements(5));
                Assert.Equal(EngagementOutcome.Completed, query.GetRecentEngagements(5)[0].Outcome);
                Assert.NotNull(query.GetCurrentHandoff());
                Assert.Contains("Continue verification", query.GenerateStatusSummary());
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            }
        }
    }
}
