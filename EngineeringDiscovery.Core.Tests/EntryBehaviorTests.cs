using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class EntryBehaviorTests
    {
        [Fact]
        public void ReplaceWorkspace_CreatesWorkspaceState()
        {
            var ws = new Workspace();
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:/repo" });
            var persistence = new InMemoryWorkspacePersistence();
            var fingerprint = new Tests.TestFingerprintService();
            var state = new WorkspaceState(persistence, fingerprint);

            Assert.False(state.HasWorkspace);
            state.ReplaceWorkspace(ws);
            Assert.True(state.HasWorkspace);
        }

        [Fact]
        public async Task PersistedWorkspace_IsResumable()
        {
            var persistence = new FileWorkspacePersistence("TestData/workspace.json");
            var ws = new Workspace();
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:/repo" });
            await persistence.SaveAsync(ws);

            var loaded = await persistence.LoadAsync();
            var persistence2 = new InMemoryWorkspacePersistence();
            var fingerprint2 = new Tests.TestFingerprintService();
            var state = new WorkspaceState(persistence2, fingerprint2);
            if (loaded is not null) state.ReplaceWorkspace(loaded);

            Assert.True(state.HasWorkspace);
            Assert.NotEmpty(state.ImportedRepositories);
        }

        [Fact]
        public void NoWorkspace_NoIntent_DoesNotCreateWorkspace()
        {
            var persistence3 = new InMemoryWorkspacePersistence();
            var fingerprint3 = new Tests.TestFingerprintService();
            var state = new WorkspaceState(persistence3, fingerprint3);
            Assert.False(state.HasWorkspace);
        }
    }
}
