using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.Activity
{
    public enum ActivityType
    {
        ProductDiscovery
    }

    public enum ActivityStatus
    {
        Created,
        Active,
        Completed,
        Archived
    }

    public abstract class EngineeringActivity
    {
        protected EngineeringActivity()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
            Observations = new List<string>();
            RecoveredUnderstanding = new List<string>();
        }

        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public ActivityType ActivityType { get; set; }
        public ActivityStatus Status { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }

        // Minimal activity-owned collections for ED-300
        public List<string> Intent { get; set; }
        public List<string> Observations { get; set; }
        public List<string> RecoveredUnderstanding { get; set; }
    }
}
