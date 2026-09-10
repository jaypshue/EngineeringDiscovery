using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    /// <summary>
    /// Assembled context package for the next worker. Agent-neutral.
    /// Contains everything a coding worker needs to begin or resume work.
    /// </summary>
    public sealed class HandoffState
    {
        public HandoffState()
        {
            Id = Guid.NewGuid();
            Objective = string.Empty;
            Context = string.Empty;
            AcceptanceCriteria = string.Empty;
            PreviousEngagementSummary = string.Empty;
            RelevantFiles = new List<string>();
            RelevantDecisionIds = new List<Guid>();
            RelevantEvidenceIds = new List<Guid>();
            Constraints = new List<string>();
            AssembledUtc = DateTime.UtcNow;
        }

        public Guid Id { get; set; }
        public DateTime AssembledUtc { get; set; }
        public Guid? TargetWorkerId { get; set; }
        public string Objective { get; set; }
        public string Context { get; set; }
        public List<string> RelevantFiles { get; set; }
        public List<Guid> RelevantDecisionIds { get; set; }
        public List<Guid> RelevantEvidenceIds { get; set; }
        public List<string> Constraints { get; set; }
        public string AcceptanceCriteria { get; set; }
        public Guid? PreviousEngagementId { get; set; }
        public string PreviousEngagementSummary { get; set; }
    }
}
