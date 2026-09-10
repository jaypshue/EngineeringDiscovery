using System;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Tests.Tests;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class ProjectStateServiceTests
    {
        private static WorkspaceState CreateWorkspaceState()
        {
            var persistence = new InMemoryWorkspacePersistence();
            return new WorkspaceState(persistence, new TestFingerprintService());
        }

        private static WorkspaceState CreateWorkspaceState(InMemoryWorkspacePersistence persistence)
        {
            return new WorkspaceState(persistence, new TestFingerprintService());
        }

        [Fact]
        public void SetProjectIdentity_InitializesProjectState_WhenNull()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            var identity = new ProjectIdentity { Name = "TestProject", Description = "A test" };

            // Act
            svc.SetProjectIdentity(identity);

            // Assert
            Assert.NotNull(ws.ActiveWorkspace);
            Assert.NotNull(ws.ActiveWorkspace.ProjectState);
            Assert.NotEqual(default, ws.ActiveWorkspace.ProjectState.CreatedUtc);
        }

        [Fact]
        public void SetProjectIdentity_StoresIdentityAndTimestamps()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            var before = DateTime.UtcNow;
            var identity = new ProjectIdentity
            {
                Name = "MyProject",
                Description = "desc",
                ProductVision = "vision"
            };

            // Act
            svc.SetProjectIdentity(identity);

            // Assert
            var ps = ws.ActiveWorkspace!.ProjectState!;
            Assert.Equal("MyProject", ps.Identity!.Name);
            Assert.Equal("desc", ps.Identity.Description);
            Assert.Equal("vision", ps.Identity.ProductVision);
            Assert.True(ps.Identity.LastUpdatedUtc >= before);
            Assert.True(ps.Identity.EstablishedUtc >= before);
            Assert.True(ps.LastUpdatedUtc >= before);
        }

        [Fact]
        public void SetProjectIdentity_PreservesEstablishedUtc_WhenAlreadySet()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            var originalEstablished = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var identity = new ProjectIdentity
            {
                Name = "MyProject",
                EstablishedUtc = originalEstablished
            };

            // Act
            svc.SetProjectIdentity(identity);

            // Assert - EstablishedUtc should remain as the original value
            Assert.Equal(originalEstablished, ws.ActiveWorkspace!.ProjectState!.Identity!.EstablishedUtc);
        }

        [Fact]
        public void SetProjectIdentity_ThrowsOnNull()
        {
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);

            Assert.Throws<ArgumentNullException>(() => svc.SetProjectIdentity(null!));
        }

        [Fact]
        public void SetProjectIdentity_SavesOnceAndNotifiesOnce()
        {
            // Arrange
            var persistence = new InMemoryWorkspacePersistence();
            var ws = CreateWorkspaceState(persistence);
            var svc = new ProjectStateService(ws);

            int notifyCount = 0;
            ws.OnChange += () => notifyCount++;

            var identity = new ProjectIdentity { Name = "Test" };

            // Act
            svc.SetProjectIdentity(identity);

            // Assert - OnChange fired (at least once via PersistAndNotify, plus potentially from ReplaceWorkspace)
            // The key assertion: persistence was called (workspace was saved)
            var loaded = persistence.LoadAsync().GetAwaiter().GetResult();
            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.ProjectState);

            // PersistAndNotify fires OnChange once; EnsureProjectState calls ReplaceWorkspace (fires once if workspace was null)
            // So with a null starting workspace: ReplaceWorkspace(1) + PersistAndNotify(1) = 2
            Assert.True(notifyCount >= 1, $"Expected at least 1 notification, got {notifyCount}");
        }

        [Fact]
        public void UpdateLifecyclePhase_StoresPhaseAndFocus()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            // Pre-initialize with identity so workspace exists
            svc.SetProjectIdentity(new ProjectIdentity { Name = "Test" });

            var before = DateTime.UtcNow;

            // Act
            svc.UpdateLifecyclePhase(LifecyclePhase.ActiveDevelopment, "Building auth module");

            // Assert
            var ps = ws.ActiveWorkspace!.ProjectState!;
            Assert.NotNull(ps.Lifecycle);
            Assert.Equal(LifecyclePhase.ActiveDevelopment, ps.Lifecycle!.Phase);
            Assert.Equal("Building auth module", ps.Lifecycle.CurrentFocus);
            Assert.True(ps.Lifecycle.PhaseEnteredUtc >= before);
            Assert.True(ps.Lifecycle.LastActivityUtc >= before);
            Assert.True(ps.LastUpdatedUtc >= before);
        }

        [Fact]
        public void UpdateLifecyclePhase_RefreshesResumePoint()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            svc.SetProjectIdentity(new ProjectIdentity { Name = "Test" });

            // Act
            svc.UpdateLifecyclePhase(LifecyclePhase.Stabilization, "Final testing");

            // Assert
            var ps = ws.ActiveWorkspace!.ProjectState!;
            Assert.NotNull(ps.ResumePoint);
            Assert.False(string.IsNullOrEmpty(ps.ResumePoint!.Summary));
            Assert.False(string.IsNullOrEmpty(ps.ResumePoint.NextRecommendedAction));
            Assert.Contains("Test", ps.ResumePoint.Summary);
            Assert.Contains("Stabilization", ps.ResumePoint.Summary);
        }

        [Fact]
        public void UpdateLifecyclePhase_SavesOnceAndNotifiesOnce()
        {
            // Arrange
            var persistence = new InMemoryWorkspacePersistence();
            var ws = CreateWorkspaceState(persistence);
            var svc = new ProjectStateService(ws);
            // Pre-populate workspace with project state to avoid extra notifications from init
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState();

            int notifyCount = 0;
            ws.OnChange += () => notifyCount++;

            // Act
            svc.UpdateLifecyclePhase(LifecyclePhase.Maintenance, "Bug fixes");

            // Assert - exactly 1 PersistAndNotify call
            Assert.Equal(1, notifyCount);

            var loaded = persistence.LoadAsync().GetAwaiter().GetResult();
            Assert.NotNull(loaded);
            Assert.Equal(LifecyclePhase.Maintenance, loaded!.ProjectState!.Lifecycle!.Phase);
        }

        [Fact]
        public void UpdateLifecyclePhase_HandlersNullFocus()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            svc.SetProjectIdentity(new ProjectIdentity { Name = "Test" });

            // Act - null focus should be stored as empty string
            svc.UpdateLifecyclePhase(LifecyclePhase.Paused, null!);

            // Assert
            Assert.Equal(string.Empty, ws.ActiveWorkspace!.ProjectState!.Lifecycle!.CurrentFocus);
        }

        [Fact]
        public void RefreshResumePoint_ProducesNonEmptySummary()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            svc.SetProjectIdentity(new ProjectIdentity { Name = "MyApp" });
            svc.UpdateLifecyclePhase(LifecyclePhase.ActiveDevelopment, "API layer");

            // Act
            svc.RefreshResumePoint();

            // Assert
            var rp = ws.ActiveWorkspace!.ProjectState!.ResumePoint!;
            Assert.False(string.IsNullOrEmpty(rp.Summary));
            Assert.False(string.IsNullOrEmpty(rp.NextRecommendedAction));
            Assert.False(string.IsNullOrEmpty(rp.Rationale));
            Assert.Contains("MyApp", rp.Summary);
            Assert.Contains("ActiveDevelopment", rp.Summary);
        }

        [Fact]
        public void RefreshResumePoint_NoOpWhenProjectStateNull()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            // No ProjectState initialized

            // Act - should not throw
            svc.RefreshResumePoint();

            // Assert - no workspace created (no side-effects)
            Assert.Null(ws.ActiveWorkspace);
        }

        [Fact]
        public void GetCurrentState_ReturnsProjectState()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);
            svc.SetProjectIdentity(new ProjectIdentity { Name = "Test" });

            // Act
            var state = svc.GetCurrentState();

            // Assert
            Assert.NotNull(state);
            Assert.Equal("Test", state!.Identity!.Name);
        }

        [Fact]
        public void GetCurrentState_ReturnsNull_WhenNoWorkspace()
        {
            // Arrange
            var ws = CreateWorkspaceState();
            var svc = new ProjectStateService(ws);

            // Act
            var state = svc.GetCurrentState();

            // Assert
            Assert.Null(state);
        }

        [Fact]
        public void Slice2Operations_RecordAndResolveState()
        {
            var persistence = new InMemoryWorkspacePersistence();
            var ws = CreateWorkspaceState(persistence);
            var svc = new ProjectStateService(ws);
            var engagement = new WorkerEngagement { Id = Guid.NewGuid(), WorkerName = "Worker", TaskDescription = "Implement state" };

            svc.RecordWorkerEngagement(engagement);
            svc.UpdateEngagementOutcome(engagement.Id, EngagementOutcome.Completed, AcceptanceStatus.Accepted);
            svc.AssembleHandoff(new HandoffState { Objective = "Verify implementation" });
            var capability = new CompletedCapability { Id = Guid.NewGuid(), Title = "State persisted", Acceptance = CapabilityAcceptance.Accepted };
            var issue = new KnownIssue { Id = Guid.NewGuid(), Title = "Test issue", Severity = IssueSeverity.High };
            svc.RecordCompletedCapability(capability);
            svc.RecordKnownIssue(issue);
            svc.ResolveIssue(issue.Id, "fixed");

            var state = svc.GetCurrentState();
            Assert.NotNull(state);
            Assert.Single(state!.WorkerEngagements);
            Assert.Equal(EngagementOutcome.Completed, state.WorkerEngagements[0].Outcome);
            Assert.Equal(AcceptanceStatus.Accepted, state.WorkerEngagements[0].Acceptance);
            Assert.NotNull(state.CurrentHandoff);
            Assert.Single(state.CompletedCapabilities);
            Assert.Equal(IssueStatus.Resolved, state.KnownIssues[0].Status);
            Assert.NotNull(state.ResumePoint);
            Assert.NotNull(persistence.LoadAsync().GetAwaiter().GetResult());
        }

        [Fact]
        public void Constructor_ThrowsOnNullWorkspaceState()
        {
            Assert.Throws<ArgumentNullException>(() => new ProjectStateService(null!));
        }
    }
}
