using System;
using System.Linq;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Tests.Tests;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class ProjectModelTests
    {
        private static WorkspaceState CreateWorkspaceState()
        {
            return new WorkspaceState(new InMemoryWorkspacePersistence(), new TestFingerprintService());
        }

        [Fact]
        public void Workspace_Projects_DefaultsToEmptyList()
        {
            var ws = new Workspace();
            Assert.NotNull(ws.Projects);
            Assert.Empty(ws.Projects);
            Assert.Null(ws.ActiveProjectId);
            Assert.Null(ws.ActiveProject);
            Assert.Null(ws.ActiveProjectState);
        }

        [Fact]
        public void ActiveProject_ReturnsCorrectProject()
        {
            var ws = new Workspace();
            var project = new Project();
            project.State.Identity = new ProjectIdentity { Name = "EngineOS" };
            ws.Projects.Add(project);
            ws.ActiveProjectId = project.Id;

            Assert.NotNull(ws.ActiveProject);
            Assert.Equal("EngineOS", ws.ActiveProject!.Name);
            Assert.NotNull(ws.ActiveProjectState);
        }

        [Fact]
        public void ActiveProject_ReturnsNull_WhenNoActiveId()
        {
            var ws = new Workspace();
            var project = new Project();
            ws.Projects.Add(project);
            // ActiveProjectId not set
            Assert.Null(ws.ActiveProject);
            Assert.Null(ws.ActiveProjectState);
        }

        [Fact]
        public void MultipleProjects_SwitchActive()
        {
            var ws = new Workspace();
            var projectA = new Project();
            projectA.State.Identity = new ProjectIdentity { Name = "ProjectA" };
            var projectB = new Project();
            projectB.State.Identity = new ProjectIdentity { Name = "ProjectB" };

            ws.Projects.Add(projectA);
            ws.Projects.Add(projectB);

            ws.ActiveProjectId = projectA.Id;
            Assert.Equal("ProjectA", ws.ActiveProject!.Name);

            ws.ActiveProjectId = projectB.Id;
            Assert.Equal("ProjectB", ws.ActiveProject!.Name);
        }

        [Fact]
        public void RemoveProject_DoesNotAffectOtherProjects()
        {
            var ws = new Workspace();
            var projectA = new Project();
            projectA.State.Identity = new ProjectIdentity { Name = "A" };
            var projectB = new Project();
            projectB.State.Identity = new ProjectIdentity { Name = "B" };

            ws.Projects.Add(projectA);
            ws.Projects.Add(projectB);
            ws.ActiveProjectId = projectA.Id;

            // Remove B
            ws.Projects.Remove(projectB);

            // A still works
            Assert.Equal("A", ws.ActiveProject!.Name);
            Assert.Single(ws.Projects);
        }

        [Fact]
        public void CloseProject_SetActiveToNull()
        {
            var ws = new Workspace();
            var project = new Project();
            ws.Projects.Add(project);
            ws.ActiveProjectId = project.Id;

            // Close = set active to null
            ws.ActiveProjectId = null;

            Assert.Null(ws.ActiveProject);
            Assert.Single(ws.Projects); // project still exists
        }

        [Fact]
        public void Migration_LegacyProjectState_PromotedToProject()
        {
            var wsState = CreateWorkspaceState();

            // Simulate a legacy workspace with ProjectState but no Projects
            var legacy = new Workspace();
            legacy.ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity { Name = "LegacyProject" }
            };
            legacy.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = @"C:\code\legacy" });

            // ReplaceWorkspace should migrate
            wsState.ReplaceWorkspace(legacy);

            Assert.NotEmpty(wsState.ActiveWorkspace!.Projects);
            Assert.NotNull(wsState.ActiveWorkspace.ActiveProjectId);
            Assert.Equal("LegacyProject", wsState.ActiveWorkspace.ActiveProject!.Name);
            Assert.Contains(@"C:\code\legacy", wsState.ActiveWorkspace.ActiveProject.RepositoryPaths);
            Assert.Equal(wsState.ActiveWorkspace.ActiveProjectId, wsState.ActiveWorkspace.ImportedRepositories[0].ProjectId);
        }

        [Fact]
        public void Migration_EmptyLegacy_NoProjectCreated()
        {
            var wsState = CreateWorkspaceState();
            var legacy = new Workspace(); // no ProjectState
            wsState.ReplaceWorkspace(legacy);

            Assert.Empty(wsState.ActiveWorkspace!.Projects);
            Assert.Null(wsState.ActiveWorkspace.ActiveProjectId);
        }

        [Fact]
        public void ProjectStateService_WritesToActiveProject()
        {
            var wsState = CreateWorkspaceState();
            var pss = new ProjectStateService(wsState);

            pss.SetProjectIdentity(new ProjectIdentity { Name = "TestProject" });

            // Should have created a project in the Projects list
            Assert.NotEmpty(wsState.ActiveWorkspace!.Projects);
            Assert.NotNull(wsState.ActiveWorkspace.ActiveProjectId);
            Assert.Equal("TestProject", wsState.ActiveWorkspace.ActiveProject!.State.Identity!.Name);
        }

        [Fact]
        public void EngineeringStateQuery_ReadsFromActiveProject()
        {
            var wsState = CreateWorkspaceState();
            var ws = new Workspace();
            var project = new Project();
            project.State.Identity = new ProjectIdentity { Name = "QueryTest" };
            ws.Projects.Add(project);
            ws.ActiveProjectId = project.Id;
            wsState.ReplaceWorkspace(ws);

            var query = new EngineeringStateQuery(wsState);
            var identity = query.GetProjectIdentity();
            Assert.NotNull(identity);
            Assert.Equal("QueryTest", identity!.Name);
        }

        [Fact]
        public void ProjectRepository_AssociatesWithProject()
        {
            var ws = new Workspace();
            var project = new Project();
            project.RepositoryPaths.Add(@"C:\projects\EngineOS");
            ws.Projects.Add(project);
            ws.ActiveProjectId = project.Id;

            Assert.Contains(@"C:\projects\EngineOS", ws.ActiveProject!.RepositoryPaths);
        }

        [Fact]
        public void ImportedRepository_Recognizes_Its_EngineOs_Project()
        {
            var ws = new Workspace();
            var project = new Project();
            project.RepositoryPaths.Add(@"C:\projects\EngineOS");
            ws.Projects.Add(project);
            ws.ImportedRepositories.Add(new ImportedRepository
            {
                RepositoryPath = @"C:\projects\EngineOS\",
                ProjectId = project.Id
            });

            var recognized = ws.FindProjectForRepositoryPath(@"c:\projects\EngineOS");

            Assert.Same(project, recognized);
            Assert.Same(ws.ImportedRepositories[0], ws.FindImportedRepositoryForProject(project.Id));
        }

        [Fact]
        public void Legacy_Repository_Path_Is_Linked_When_Project_Match_Is_Unambiguous()
        {
            var wsState = CreateWorkspaceState();
            var ws = new Workspace();
            var project = new Project();
            project.RepositoryPaths.Add(@"C:\projects\EngineOS");
            ws.Projects.Add(project);
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = @"C:\projects\EngineOS\" });

            wsState.ReplaceWorkspace(ws);

            Assert.Equal(project.Id, wsState.ActiveWorkspace!.ImportedRepositories[0].ProjectId);
        }
    }
}
