using System;
using EngineeringDiscovery.Core.Domain.CurrentTask;
using EngineeringDiscovery.Web.Services;

namespace EngineeringDiscovery.Web.Services
{
    public sealed class CurrentTaskState
    {
        public CurrentTask? ActiveTask { get; private set; }

        public event Action? OnChange;

        // Called by WorkspaceState during startup to seed the active task from persisted workspace
        public void SeedFromWorkspace(CurrentTask? task)
        {
            ActiveTask = task;
            NotifyStateChanged();
        }


        public CurrentTask StartTask(string title, string description, string goal)
        {
            var task = new CurrentTask(title, description, goal);
            ActiveTask = task;
            NotifyStateChanged();
            return task;
        }

        public void UpdateBrief(Action<EngineeringDiscovery.Core.Domain.CurrentTask.EngineeringBrief> update)
        {
            if (ActiveTask is null) return;

            update(ActiveTask.Brief);
            // Trace update for debugging propagation issues
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = System.IO.Path.Combine(localAppData, "EngineeringDiscovery");
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                var logPath = System.IO.Path.Combine(dir, "currenttask_updates.log");
                var msg = $"{DateTime.UtcNow:o} UpdateBrief invoked. Objective='{ActiveTask.Brief.Objective}' Notes='{ActiveTask.Brief.Notes}' Implementation='{ActiveTask.Brief.ImplementationThoughts}'\n";
                System.IO.File.AppendAllText(logPath, msg);
            }
            catch { }

            ActiveTask = ActiveTask; // keep reference but indicate mutation
            NotifyStateChanged();
        }

        public void CompleteTask()
        {
            if (ActiveTask is null) return;

            ActiveTask.Complete();
            ActiveTask = null;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
