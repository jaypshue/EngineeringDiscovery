using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    /// <summary>
    /// Records a worker's interaction with a work item. Agent-neutral.
    /// </summary>
    public enum EngagementOutcome
    {
        InProgress,
        Completed,
        PartiallyCompleted,
        Failed,
        Escalated
    }

    public enum AcceptanceStatus
    {
        Pending,
        Accepted,
        Rejected,
        Superseded
    }

    public sealed class WorkerEngagement
    {
        public WorkerEngagement()
        {
            Id = Guid.NewGuid();
            WorkerName = string.Empty;
            TaskDescription = string.Empty;
            Summary = string.Empty;
            AcceptanceReason = string.Empty;
            FilesChanged = new List<string>();
            EvidenceProduced = new List<Guid>();
            StartedUtc = DateTime.UtcNow;
            Outcome = EngagementOutcome.InProgress;
            Acceptance = AcceptanceStatus.Pending;
        }

        public Guid Id { get; set; }
        public Guid WorkerId { get; set; }
        public string WorkerName { get; set; }
        public Guid? WorkItemId { get; set; }
        public string TaskDescription { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime? CompletedUtc { get; set; }
        public EngagementOutcome Outcome { get; set; }
        public string Summary { get; set; }
        public List<string> FilesChanged { get; set; }
        public List<Guid> EvidenceProduced { get; set; }
        public AcceptanceStatus Acceptance { get; set; }
        public string AcceptanceReason { get; set; }
        public Guid? EscalatedToWorkerId { get; set; }
    }
}
