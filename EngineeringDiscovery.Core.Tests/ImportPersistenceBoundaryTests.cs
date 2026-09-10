using System;
using System.IO;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Tests.Tests;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public sealed class ImportPersistenceBoundaryTests
    {
        [Fact]
        public void Save_ReturnsFalseWhenPersistenceFails_WithoutDiscardingInMemoryState()
        {
            var persistence = new ThrowingWorkspacePersistence();
            var state = new WorkspaceState(persistence, new TestFingerprintService());
            var workspace = new Workspace();
            workspace.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = @"C:\repo" });
            state.ReplaceWorkspace(workspace);

            var persisted = state.Save();

            Assert.False(persisted);
            Assert.Same(workspace, state.ActiveWorkspace);
            Assert.Single(state.ActiveWorkspace!.ImportedRepositories);
        }

        [Fact]
        public async Task ImportedProjectInvestigationAndFreshnessSurviveReload()
        {
            var folder = Path.Combine(Path.GetTempPath(), "EngineOS-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var persistence = new FileWorkspacePersistence(folder);
                var state = new WorkspaceState(persistence, new TestFingerprintService());
                var investigation = Investigation.Create(Guid.NewGuid(), @"C:\repo");
                var builtUtc = DateTime.UtcNow;
                var fingerprint = "repo-fingerprint";

                var workspace = new Workspace
                {
                    Investigation = investigation
                };
                workspace.ImportedRepositories.Add(new ImportedRepository
                {
                    RepositoryPath = @"C:\repo",
                    Investigation = investigation,
                    LastBuiltUtc = builtUtc,
                    RepositoryFingerprint = fingerprint
                });

                var project = new Project();
                project.State.Identity = new ProjectIdentity { Name = "repo" };
                project.RepositoryPaths.Add(@"C:\repo");
                workspace.Projects.Add(project);
                workspace.ActiveProjectId = project.Id;
                workspace.ImportedRepositories[0].ProjectId = project.Id;
                workspace.SetFreshness(builtUtc, fingerprint);

                state.ReplaceWorkspace(workspace);
                Assert.True(state.Save());

                var reloadedState = new WorkspaceState(persistence, new TestFingerprintService());
                var reloaded = await persistence.LoadAsync();
                Assert.NotNull(reloaded);
                reloadedState.ReplaceWorkspace(reloaded!);

                Assert.NotNull(reloadedState.ActiveWorkspace!.ActiveProject);
                Assert.Equal(project.Id, reloadedState.ActiveWorkspace.ActiveProjectId);
                Assert.Equal("repo", reloadedState.ActiveWorkspace.ActiveProject!.State.Identity!.Name);
                Assert.Equal(investigation.Id, reloadedState.ActiveWorkspace.Investigation!.Id);
                Assert.Equal(@"C:\repo", reloadedState.ActiveWorkspace.ActiveProject.RepositoryPaths[0]);
                Assert.Equal(project.Id, reloadedState.ActiveWorkspace.ImportedRepositories[0].ProjectId);
                Assert.Equal(builtUtc, reloadedState.ActiveWorkspace.LastBuiltUtc);
                Assert.Equal(fingerprint, reloadedState.ActiveWorkspace.RepositoryFingerprint);
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }

        private sealed class ThrowingWorkspacePersistence : IWorkspacePersistence
        {
            public Task<Workspace?> LoadAsync() => Task.FromResult<Workspace?>(null);

            public Task SaveAsync(Workspace? workspace) =>
                Task.FromException(new IOException("test persistence failure"));
        }
    }
}
