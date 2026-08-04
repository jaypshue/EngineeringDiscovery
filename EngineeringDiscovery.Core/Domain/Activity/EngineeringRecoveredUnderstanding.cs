using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.Activity
{
    public sealed class EngineeringRecoveredUnderstanding
    {
        public EngineeringRecoveredUnderstanding()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            Statement = string.Empty;
            Confidence = 0;
            SupportingEvidenceIds = new List<Guid>();
        }

        public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Statement { get; set; }
        // 0-100 percentage
        public int Confidence { get; set; }
        public List<Guid> SupportingEvidenceIds { get; set; }
    }
}
