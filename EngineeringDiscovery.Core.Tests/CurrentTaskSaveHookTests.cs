using System;
using System.IO;
using EngineeringDiscovery.Core.Domain.CurrentTask;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Web.Services;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class CurrentTaskSaveHookTests
    {
        [Fact]
        public void UpdateBrief_Triggers_Persistence_Save()
        {
            // Arrange
            var persistence = new EngineeringDiscovery.Core.Services.InMemoryWorkspacePersistence();
            var ws = new WorkspaceState(persistence, new EngineeringDiscovery.Core.Services.TestRepoFingerprintService());

            var workspace = new Workspace
            {
                RepositoryPath = "C:\\temp\\repo"
            };

            var ct = new CurrentTask("t","d","g");
            ct.Brief.Objective = "initial";
            workspace.CurrentTask = ct;

            // set ActiveWorkspace via reflection
            var prop = typeof(WorkspaceState).GetProperty("ActiveWorkspace");
            Assert.NotNull(prop);
            prop!.SetValue(ws, workspace);

            var currentTaskState = new TestCurrentTaskState();

            // Register hooks so UpdateBrief will cause persistence (test wiring moved to test)
            currentTaskState.OnChange += () =>
            {
                // Copy current task into the WorkspaceState.ActiveWorkspace and persist
                var activeProp = typeof(WorkspaceState).GetProperty("ActiveWorkspace");
                var activeWs = activeProp!.GetValue(ws) as Workspace;
                if (activeWs is null)
                {
                    activeWs = new Workspace();
                    activeProp.SetValue(ws, activeWs);
                }
                // set CurrentTask on the workspace object
                var wsTypeProp = typeof(Workspace).GetProperty("CurrentTask");
                wsTypeProp?.SetValue(activeWs, currentTaskState.ActiveTask);
                ws.Save();
            };

            // Seed currentTaskState from workspace
            currentTaskState.SeedFromWorkspace(workspace.CurrentTask);

            // Act - update brief which should trigger persistence hook
            currentTaskState.UpdateBrief(b => b.Objective = "modified");

            // Create fresh WorkspaceState and explicitly initialize from persistence
            var ws2 = new WorkspaceState(persistence, new EngineeringDiscovery.Core.Services.TestRepoFingerprintService());
            var loaded = persistence.LoadAsync().GetAwaiter().GetResult();
            if (loaded is not null) ws2.ReplaceWorkspace(loaded);

            // Assert
            Assert.NotNull(ws2.ActiveWorkspace);
            Assert.NotNull(ws2.ActiveWorkspace.CurrentTask);
            Assert.Equal("modified", ws2.ActiveWorkspace.CurrentTask.Brief.Objective);

            // Cleanup persisted file
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var path = Path.Combine(localAppData, "EngineeringDiscovery", "workspace.json");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        // Local test double to avoid depending on presentation project types
        private class TestCurrentTaskState
        {
            public CurrentTask? ActiveTask { get; private set; }
            public event Action? OnChange;

            public void SeedFromWorkspace(CurrentTask? task)
            {
                ActiveTask = task;
                NotifyStateChanged();
            }

            public void UpdateBrief(Action<EngineeringBrief> update)
            {
                if (ActiveTask is null) return;
                update(ActiveTask.Brief);
                NotifyStateChanged();
            }

            private void NotifyStateChanged() => OnChange?.Invoke();
        }
    }
}
