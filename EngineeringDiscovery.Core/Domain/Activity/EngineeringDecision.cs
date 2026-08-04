using System;

namespace EngineeringDiscovery.Core.Domain.Activity
{
    public enum DecisionStatus
    {
        Proposed,
        Accepted,
        Rejected
    }

    public sealed class EngineeringDecision
    {
        public EngineeringDecision()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            Statement = string.Empty;
            Status = DecisionStatus.Proposed;
            Confidence = 0;
            ResponsibleActorId = string.Empty;
            SupportingRecoveredUnderstandingIds = new System.Collections.Generic.List<Guid>();
        }

        public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Statement { get; set; }
        public DecisionStatus Status { get; set; }
        // 0-100 percentage
        public int Confidence { get; set; }
        public string ResponsibleActorId { get; set; }
        public System.Collections.Generic.List<Guid> SupportingRecoveredUnderstandingIds { get; set; }
    }
}
