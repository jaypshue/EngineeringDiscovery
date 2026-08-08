using System;

namespace EngineeringDiscovery.Web.Models
{
    // Minimal in-session Work Contract model for ED-303
    public class WorkContract
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string RepositoryName { get; set; } = string.Empty;
        public string RepositoryPath { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public string AcceptanceCriteria { get; set; } = string.Empty;
        public string OutOfScope { get; set; } = string.Empty;
        public string ImplementationPlan { get; set; } = string.Empty;
        public string VerificationRequirements { get; set; } = string.Empty;
        public string Status { get; set; } = "Editing"; // Editing | Ready
        public bool HumanReady { get; set; }
        public bool EngineOSReady { get; set; }
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
