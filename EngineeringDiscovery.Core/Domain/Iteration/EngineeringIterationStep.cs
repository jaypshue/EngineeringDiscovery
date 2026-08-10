using System;

namespace EngineeringDiscovery.Core.Domain.Iteration
{
    public sealed class EngineeringIterationStep
    {
        public EngineeringIterationStep()
        {
            Id = Guid.NewGuid();
            Prompt = string.Empty;
            CopilotResponse = string.Empty;
            HumanObservation = string.Empty;
            EvidenceReference = string.Empty;
            Assessment = string.Empty;
            NextAction = string.Empty;
            CreatedUtc = DateTime.UtcNow;
        }

        public Guid Id { get; set; }
        public string Prompt { get; set; }
        public string CopilotResponse { get; set; }
        public string HumanObservation { get; set; }
        public string EvidenceReference { get; set; }
        public string Assessment { get; set; }
        public string NextAction { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
