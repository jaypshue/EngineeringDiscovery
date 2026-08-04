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
        public void UpdateBrief_Triggers_RegisterPersistenceHooks_Save()
        {
            // Arrange
            var ws = new WorkspaceState();

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

            // Register hooks so UpdateBrief will cause Save
            ws.RegisterPersistenceHooks(currentTaskState, null);

            // Seed currentTaskState from workspace
            currentTaskState.SeedFromWorkspace(workspace.CurrentTask);

            // Act - update brief which should trigger persistence hook
            currentTaskState.UpdateBrief(b => b.Objective = "modified");

            // Create fresh WorkspaceState to load what's persisted
            var ws2 = new WorkspaceState();

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
