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
            HypothesisSpace = new List<EngineeringHypothesis>();
            EvidenceRequests = new List<EngineeringEvidenceRequest>();
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
        public List<EngineeringHypothesis> HypothesisSpace { get; set; }
        public List<EngineeringEvidenceRequest> EvidenceRequests { get; set; }
        public List<EngineeringEvidence> Evidence { get; set; }

        // Convenience: current observation (most recent)
        public EngineeringObservation? CurrentObservation => Observations.Count > 0 ? Observations[^1] : null;

        // Hypothesis convenience
        public EngineeringHypothesis? CurrentHypothesis => HypothesisSpace.Count > 0 ? HypothesisSpace[^1] : null;

        public void AddHypothesis(EngineeringHypothesis h)
        {
            if (h is null) return;
            HypothesisSpace.Add(h);
            UpdatedUtc = DateTime.UtcNow;
        }

        public EngineeringEvidenceRequest? CurrentEvidenceRequest => EvidenceRequests.Count > 0 ? EvidenceRequests[^1] : null;

        public void AddEvidenceRequest(EngineeringEvidenceRequest r)
        {
            if (r is null) return;
            EvidenceRequests.Add(r);
            UpdatedUtc = DateTime.UtcNow;
        }

        public void AddEvidence(EngineeringEvidence e)
        {
            if (e is null) return;
            Evidence.Add(e);
            UpdatedUtc = DateTime.UtcNow;
        }

        public void AddObservation(EngineeringObservation obs)
        {
            if (obs is null) return;
            Observations.Add(obs);
            UpdatedUtc = DateTime.UtcNow;
        }
    }
}
