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

    public enum ObservationType
    {
        Product,
        Repository,
        Architecture,
        Implementation,
        Testing,
        Recovery
    }

    public enum ObservationSource
    {
        Human,
        EngineOS,
        AI,
        System
    }

    public sealed class EngineeringObservation
    {
        public EngineeringObservation()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            Description = string.Empty;
            Confidence = 0;
            Source = ObservationSource.Human;
            ObservationType = ObservationType.Product;
        }

        public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Description { get; set; }
        public ObservationType ObservationType { get; set; }
        public ObservationSource Source { get; set; }
        // 0-100 percentage
        public int Confidence { get; set; }
    }

    public abstract class EngineeringActivity
    {
        protected EngineeringActivity()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
            Intent = new List<string>();
            Observations = new List<EngineeringObservation>();
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
        public List<EngineeringObservation> Observations { get; set; }
        public List<string> RecoveredUnderstanding { get; set; }

        // Convenience: current observation (most recent)
        public EngineeringObservation? CurrentObservation => Observations.Count > 0 ? Observations[^1] : null;

        public void AddObservation(EngineeringObservation obs)
        {
            if (obs is null) return;
            Observations.Add(obs);
            UpdatedUtc = DateTime.UtcNow;
        }
    }
}
