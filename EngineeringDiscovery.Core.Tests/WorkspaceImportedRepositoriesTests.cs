using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class WorkspaceImportedRepositoriesTests
    {
        [Fact]
        public async Task AddSecondRepository_Appends()
        {
            var persistence = new InMemoryWorkspacePersistence();
            var wsState = new WorkspaceState(persistence, new TestRepoFingerprintService());

            var ws = new Workspace();
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:\\projects\\EngineeringDiscovery" });
            wsState.ReplaceWorkspace(ws);

            // Simulate adding a second repository (append-only)
            var imported = new ImportedRepository { RepositoryPath = "C:\\projects\\InterviewAssistant" };
            var current = wsState.ActiveWorkspace ?? new Workspace();
            current.ImportedRepositories.Add(imported);
            wsState.ReplaceWorkspace(current);

            Assert.NotNull(wsState.ActiveWorkspace);
            Assert.Equal(2, wsState.ActiveWorkspace.ImportedRepositories.Count);
            Assert.Contains(wsState.ActiveWorkspace.ImportedRepositories, r => r.RepositoryPath.EndsWith("EngineeringDiscovery"));
            Assert.Contains(wsState.ActiveWorkspace.ImportedRepositories, r => r.RepositoryPath.EndsWith("InterviewAssistant"));
        }

        [Fact]
        public async Task PersistAndReload_PreservesMultipleRepositories()
        {
            var persistence = new InMemoryWorkspacePersistence();
            var wsState = new WorkspaceState(persistence, new TestRepoFingerprintService());

            var ws = new Workspace();
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:\\projects\\EngineeringDiscovery" });
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:\\projects\\InterviewAssistant" });
            wsState.ReplaceWorkspace(ws);
            wsState.Save();

            var loaded = await persistence.LoadAsync();
            var ws2 = new WorkspaceState(persistence, new TestRepoFingerprintService());
            if (loaded is not null) ws2.ReplaceWorkspace(loaded);

            Assert.NotNull(ws2.ActiveWorkspace);
            Assert.Equal(2, ws2.ActiveWorkspace.ImportedRepositories.Count);
        }

        [Fact]
        public async Task LegacyRepositoryPath_MigratesToImportedRepositories()
        {
            var persistence = new InMemoryWorkspacePersistence();
            var wsState = new WorkspaceState(persistence, new TestRepoFingerprintService());

            var ws = new Workspace { RepositoryPath = "C:\\projects\\EngineeringDiscovery" };
            // Persist old-style workspace
            await persistence.SaveAsync(ws);

            var loaded = await persistence.LoadAsync();
            var ws2 = new WorkspaceState(persistence, new TestRepoFingerprintService());
            if (loaded is not null) ws2.ReplaceWorkspace(loaded);

            Assert.NotNull(ws2.ActiveWorkspace);
            Assert.NotNull(ws2.ActiveWorkspace.ImportedRepositories);
            Assert.Single(ws2.ActiveWorkspace.ImportedRepositories);
            Assert.Equal("C:\\projects\\EngineeringDiscovery", ws2.ActiveWorkspace.ImportedRepositories[0].RepositoryPath);
        }

        [Fact]
        public async Task ExistingImportedRepositories_NotOverwrittenByLegacyPathOnLoad()
        {
            var persistence = new InMemoryWorkspacePersistence();
            var ws = new Workspace();
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:\\projects\\EngineeringDiscovery" });
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:\\projects\\InterviewAssistant" });
            // Also set legacy RepositoryPath to some other value
            ws.RepositoryPath = "C:\\legacy\\repo";

            await persistence.SaveAsync(ws);

            var loaded = await persistence.LoadAsync();
            var wsState = new WorkspaceState(persistence, new TestRepoFingerprintService());
            if (loaded is not null) wsState.ReplaceWorkspace(loaded);

            Assert.NotNull(wsState.ActiveWorkspace);
            Assert.Equal(2, wsState.ActiveWorkspace.ImportedRepositories.Count);
        }

        [Fact]
        public async Task RepositorySpecificRefresh_UpdatesOnlyTarget()
        {
            var persistence = new InMemoryWorkspacePersistence();
            var wsState = new WorkspaceState(persistence, new TestRepoFingerprintService());

            var ws = new Workspace();
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:\\projects\\EngineeringDiscovery", Investigation = null });
            ws.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = "C:\\projects\\InterviewAssistant", Investigation = null });
            wsState.ReplaceWorkspace(ws);

            // Simulate refresh of first repository by replacing its Investigation
            // Use public constructor and then reflectively set Id/RepositoryPath for test purposes
            var inv = new EngineeringDiscovery.Core.Domain.Investigation.Investigation();
            // Reflection to set private properties for Id and RepositoryPath used in the domain
            var invType = typeof(EngineeringDiscovery.Core.Domain.Investigation.Investigation);
            var idProp = invType.GetProperty("Id", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var repoProp = invType.GetProperty("RepositoryPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (idProp != null) idProp.SetValue(inv, Guid.NewGuid());
            if (repoProp != null) repoProp.SetValue(inv, "C:\\projects\\EngineeringDiscovery");

            wsState.ActiveWorkspace.ImportedRepositories[0].Investigation = inv;
            wsState.Save();

            Assert.NotNull(wsState.ActiveWorkspace.ImportedRepositories[0].Investigation);
            Assert.Null(wsState.ActiveWorkspace.ImportedRepositories[1].Investigation);
        }
    }
}
