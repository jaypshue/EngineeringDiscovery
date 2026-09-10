using System;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Activity;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Tests.Tests;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class EngineeringStateQueryTests
    {
        private static WorkspaceState CreateWorkspaceState()
        {
            var persistence = new InMemoryWorkspacePersistence();
            return new WorkspaceState(persistence, new TestFingerprintService());
        }

        // --- Null ProjectState tests ---

        [Fact]
        public void GetProjectIdentity_ReturnsNull_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            Assert.Null(query.GetProjectIdentity());
        }

        [Fact]
        public void GetLifecycle_ReturnsNull_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            Assert.Null(query.GetLifecycle());
        }

        [Fact]
        public void GetCompletedCapabilities_ReturnsEmpty_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            var result = query.GetCompletedCapabilities();
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetOpenIssues_ReturnsEmpty_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            var result = query.GetOpenIssues();
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetDecisions_ReturnsEmpty_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            var result = query.GetDecisions();
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetRecentEngagements_ReturnsEmpty_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            var result = query.GetRecentEngagements(5);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void GetResumePoint_ReturnsNull_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            Assert.Null(query.GetResumePoint());
        }

        [Fact]
        public void GetCurrentHandoff_ReturnsNull_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            Assert.Null(query.GetCurrentHandoff());
        }

        [Fact]
        public void GenerateStatusSummary_ReturnsNonEmpty_WhenNoProjectState()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);
            var summary = query.GenerateStatusSummary();
            Assert.False(string.IsNullOrEmpty(summary));
            Assert.Contains("No project state", summary);
        }

        // --- Populated ProjectState tests ---

        [Fact]
        public void GetProjectIdentity_ReturnsIdentity_WhenPopulated()
        {
            var ws = CreateWorkspaceState();
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity { Name = "TestApp", Description = "A test app" }
            };

            var query = new EngineeringStateQuery(ws);
            var identity = query.GetProjectIdentity();
            Assert.NotNull(identity);
            Assert.Equal("TestApp", identity!.Name);
        }

        [Fact]
        public void GetLifecycle_ReturnsLifecycle_WhenPopulated()
        {
            var ws = CreateWorkspaceState();
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                Lifecycle = new ProjectLifecycle
                {
                    Phase = LifecyclePhase.ActiveDevelopment,
                    CurrentFocus = "Auth module"
                }
            };

            var query = new EngineeringStateQuery(ws);
            var lifecycle = query.GetLifecycle();
            Assert.NotNull(lifecycle);
            Assert.Equal(LifecyclePhase.ActiveDevelopment, lifecycle!.Phase);
            Assert.Equal("Auth module", lifecycle.CurrentFocus);
        }

        [Fact]
        public void GetDecisions_ReturnsDecisions_WhenPopulated()
        {
            var ws = CreateWorkspaceState();
            ws.ReplaceWorkspace(new Workspace());
            var decision = new EngineeringDecision { Statement = "Use event sourcing", Status = DecisionStatus.Accepted };
            ws.ActiveWorkspace!.ProjectState = new ProjectState();
            ws.ActiveWorkspace.ProjectState.Decisions.Add(decision);

            var query = new EngineeringStateQuery(ws);
            var decisions = query.GetDecisions();
            Assert.Single(decisions);
            Assert.Equal("Use event sourcing", decisions[0].Statement);
        }

        [Fact]
        public void GetResumePoint_ReturnsResumePoint_WhenPopulated()
        {
            var ws = CreateWorkspaceState();
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                ResumePoint = new ResumePoint
                {
                    Summary = "Working on auth",
                    NextRecommendedAction = "Continue auth implementation"
                }
            };

            var query = new EngineeringStateQuery(ws);
            var rp = query.GetResumePoint();
            Assert.NotNull(rp);
            Assert.Equal("Working on auth", rp!.Summary);
        }

        [Fact]
        public void GenerateStatusSummary_IncludesIdentityAndLifecycle_WhenPopulated()
        {
            var ws = CreateWorkspaceState();
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity { Name = "InterviewReadyAI", Description = "Interview prep tool" },
                Lifecycle = new ProjectLifecycle
                {
                    Phase = LifecyclePhase.Stabilization,
                    CurrentFocus = "Desktop overlay polish"
                },
                ResumePoint = new ResumePoint
                {
                    NextRecommendedAction = "Finish overlay animations"
                }
            };

            var query = new EngineeringStateQuery(ws);
            var summary = query.GenerateStatusSummary();
            Assert.Contains("InterviewReadyAI", summary);
            Assert.Contains("Stabilization", summary);
            Assert.Contains("Desktop overlay polish", summary);
            Assert.Contains("Finish overlay animations", summary);
        }

        [Fact]
        public void NoQueryMethod_Throws_WhenProjectStateNull()
        {
            var ws = CreateWorkspaceState();
            var query = new EngineeringStateQuery(ws);

            // None of these should throw
            query.GetProjectIdentity();
            query.GetLifecycle();
            query.GetCompletedCapabilities();
            query.GetOpenIssues();
            query.GetDecisions();
            query.GetRecentEngagements(10);
            query.GetResumePoint();
            query.GetCurrentHandoff();
            query.GenerateStatusSummary();
        }

        [Fact]
        public void Constructor_ThrowsOnNull()
        {
            Assert.Throws<ArgumentNullException>(() => new EngineeringStateQuery(null!));
        }

        [Fact]
        public void WorkspaceContext_Uses_Active_Project_Folder_And_Investigation()
        {
            var ws = new Workspace
            {
                RepositoryPath = @"C:\projects\First"
            };
            var firstProject = new Project();
            firstProject.RepositoryPaths.Add(@"C:\projects\First");
            var secondProject = new Project();
            secondProject.RepositoryPaths.Add(@"C:\projects\Second");
            var firstInvestigation = Investigation.Create(Guid.NewGuid(), @"C:\projects\First");
            var secondInvestigation = Investigation.Create(Guid.NewGuid(), @"C:\projects\Second");

            ws.Projects.Add(firstProject);
            ws.Projects.Add(secondProject);
            ws.ImportedRepositories.Add(new ImportedRepository
            {
                RepositoryPath = @"C:\projects\First",
                ProjectId = firstProject.Id,
                Investigation = firstInvestigation
            });
            ws.ImportedRepositories.Add(new ImportedRepository
            {
                RepositoryPath = @"C:\projects\Second",
                ProjectId = secondProject.Id,
                Investigation = secondInvestigation
            });
            ws.Investigation = firstInvestigation;
            ws.ActiveProjectId = secondProject.Id;

            var state = CreateWorkspaceState();
            state.ReplaceWorkspace(ws);
            var query = new EngineeringStateQuery(state);

            var context = query.GetWorkspaceContext();

            Assert.NotNull(context);
            Assert.Equal(@"C:\projects\Second", context!.RepositoryPath);
            Assert.Equal(secondInvestigation.Id, query.GetActiveInvestigation()!.Id);
        }
    }
}
