using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    /// <summary>
    /// Captures the "where to continue" state for project resumption after inactivity.
    /// Answers "what should I do next." Auto-refreshed on every significant state mutation.
    /// </summary>
    public sealed class ResumePoint
    {
        public ResumePoint()
        {
            CapturedUtc = DateTime.UtcNow;
            Summary = string.Empty;
            NextRecommendedAction = string.Empty;
            Rationale = string.Empty;
            BlockingIssueIds = new List<Guid>();
        }

        public DateTime CapturedUtc { get; set; }
        public string Summary { get; set; }
        public string NextRecommendedAction { get; set; }
        public string Rationale { get; set; }
        public List<Guid> BlockingIssueIds { get; set; }
        public Guid? ActiveWorkItemId { get; set; }
        public Guid? RecommendedWorkerId { get; set; }
    }
}
