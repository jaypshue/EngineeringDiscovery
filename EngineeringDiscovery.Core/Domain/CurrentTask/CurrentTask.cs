using System;

namespace EngineeringDiscovery.Core.Domain.CurrentTask
{
    public enum CurrentTaskStatus
    {
        Active,
        Completed
    }

    public sealed class CurrentTask
    {
        public CurrentTask(string title, string description, string goal)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));

            Title = title.Trim();
            Description = description?.Trim() ?? string.Empty;
            Goal = goal?.Trim() ?? string.Empty;
            Status = CurrentTaskStatus.Active;
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
            Brief = new EngineeringBrief();
        }

        public string Title { get; private set; }

        public string Description { get; private set; }

        public string Goal { get; private set; }

        public CurrentTaskStatus Status { get; private set; }

        public DateTime CreatedUtc { get; private set; }

        public DateTime UpdatedUtc { get; private set; }

        public EngineeringBrief Brief { get; private set; }

        public void Complete()
        {
            if (Status == CurrentTaskStatus.Completed) return;

            Status = CurrentTaskStatus.Completed;
            UpdatedUtc = DateTime.UtcNow;
        }
    }
}
