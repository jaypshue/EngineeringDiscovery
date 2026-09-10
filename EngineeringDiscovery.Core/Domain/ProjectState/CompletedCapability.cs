using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    public enum CapabilityAcceptance
    {
        Pending,
        Accepted,
        Rejected,
        Superseded
    }

    /// <summary>
    /// A capability verified as present or complete for the project.
    /// </summary>
    public sealed class CompletedCapability
    {
        public CompletedCapability()
        {
            Id = Guid.NewGuid();
            Title = string.Empty;
            Description = string.Empty;
            SupportingEvidenceIds = new List<Guid>();
            SupportingDecisionIds = new List<Guid>();
            Acceptance = CapabilityAcceptance.Pending;
            CompletedUtc = DateTime.UtcNow;
        }

        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime CompletedUtc { get; set; }
        public List<Guid> SupportingEvidenceIds { get; set; }
        public List<Guid> SupportingDecisionIds { get; set; }
        public Guid? CompletedByWorkerId { get; set; }
        public CapabilityAcceptance Acceptance { get; set; }
    }
}
