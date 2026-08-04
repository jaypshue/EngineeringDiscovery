using System;

namespace EngineeringDiscovery.Core.Domain.Activity
{
    public sealed class EngineeringEvidenceRequest
    {
        public EngineeringEvidenceRequest()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            Target = string.Empty;
            Reason = string.Empty;
            ExpectedInformationGain = 0;
            ExpectedConfidenceIncrease = 0;
        }

        public Guid Id { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string Target { get; set; }
        public string Reason { get; set; }
        // 0-100 percentage
        public int ExpectedInformationGain { get; set; }
        // 0-100 percentage
        public int ExpectedConfidenceIncrease { get; set; }
    }
}
