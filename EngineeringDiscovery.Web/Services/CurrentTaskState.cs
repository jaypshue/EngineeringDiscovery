using System;
using EngineeringDiscovery.Core.Domain.CurrentTask;

namespace EngineeringDiscovery.Web.Services
{
    public sealed class CurrentTaskState
    {
        public CurrentTask? ActiveTask { get; private set; }

        public event Action? OnChange;

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
