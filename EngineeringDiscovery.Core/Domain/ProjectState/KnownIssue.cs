using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    public enum IssueSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum IssueStatus
    {
        Open,
        InProgress,
        Resolved,
        WontFix
    }

    /// <summary>
    /// A durable issue that can affect the project’s next action.
    /// </summary>
    public sealed class KnownIssue
    {
        public KnownIssue()
        {
            Id = Guid.NewGuid();
            Title = string.Empty;
            Description = string.Empty;
            RelatedEvidenceIds = new List<Guid>();
            Severity = IssueSeverity.Medium;
            Status = IssueStatus.Open;
            ReportedUtc = DateTime.UtcNow;
        }

        public Guid Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public IssueSeverity Severity { get; set; }
        public IssueStatus Status { get; set; }
        public DateTime ReportedUtc { get; set; }
        public DateTime? ResolvedUtc { get; set; }
        public List<Guid> RelatedEvidenceIds { get; set; }
        public Guid? AssignedWorkerId { get; set; }
    }
}
