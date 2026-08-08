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
        // Who last updated the contract ("You" | "EngineOS" | other label)
        public string LastUpdatedBy { get; set; } = string.Empty;

        public bool IsEmpty()
        {
            return string.IsNullOrWhiteSpace(Title)
                && string.IsNullOrWhiteSpace(Objective)
                && string.IsNullOrWhiteSpace(AcceptanceCriteria)
                && string.IsNullOrWhiteSpace(OutOfScope)
                && string.IsNullOrWhiteSpace(ImplementationPlan)
                && string.IsNullOrWhiteSpace(VerificationRequirements);
        }

        // The living document representation of the Work Contract. Prefer this for editing.
        public string DocumentText { get; set; } = string.Empty;

        public string ToDocumentText()
        {
            // Basic markdown-like composition
            return $"# {Title}\n\n## Objective\n{Objective}\n\n## Acceptance Criteria\n{AcceptanceCriteria}\n\n## Out Of Scope\n{OutOfScope}\n\n## Implementation Plan\n{ImplementationPlan}\n\n## Verification Requirements\n{VerificationRequirements}";
        }
    }
}
