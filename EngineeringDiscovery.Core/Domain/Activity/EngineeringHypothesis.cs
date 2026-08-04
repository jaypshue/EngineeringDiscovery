using System;

namespace EngineeringDiscovery.Core.Domain.Activity
{
    public enum HypothesisStatus
    {
        Active,
        Eliminated,
        Confirmed
    }

    public sealed class EngineeringHypothesis
    {
        public EngineeringHypothesis()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
            Description = string.Empty;
            Status = HypothesisStatus.Active;
            Confidence = 0;
        }

        public Guid Id { get; set; }
        public string Description { get; set; }
        public HypothesisStatus Status { get; set; }
        public int Confidence { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
