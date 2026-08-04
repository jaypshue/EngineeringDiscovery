using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.Activity
{
    public sealed class EngineeringEvidence
    {
        public EngineeringEvidence()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            Description = string.Empty;
            Source = string.Empty;
            FulfilledRequestIds = new List<Guid>();
        }

        public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        // References to EvidenceRequest Ids that this evidence fulfills
        public List<Guid> FulfilledRequestIds { get; set; }
    }
}
