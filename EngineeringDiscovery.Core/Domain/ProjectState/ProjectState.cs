using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    /// <summary>
    /// Aggregate root for project-level engineering state. Captures durable project identity,
    /// lifecycle status, completed capabilities, known issues, decision history, worker engagement
    /// history, and resume context.
    /// </summary>
    public sealed class ProjectState
    {
        public ProjectState()
        {
            Id = Guid.NewGuid();
            CreatedUtc = DateTime.UtcNow;
            LastUpdatedUtc = CreatedUtc;
            CompletedCapabilities = new List<CompletedCapability>();
            KnownIssues = new List<KnownIssue>();
            Decisions = new List<global::EngineeringDiscovery.Core.Domain.Activity.EngineeringDecision>();
            WorkerEngagements = new List<WorkerEngagement>();
            RegisteredWorkers = new List<WorkerProfile>();
        }

        public Guid Id { get; set; }
        public ProjectIdentity? Identity { get; set; }
        public ProjectLifecycle? Lifecycle { get; set; }

        public List<CompletedCapability> CompletedCapabilities { get; set; }

        public List<KnownIssue> KnownIssues { get; set; }

        // Project-level decisions promoted from completed activities
        public List<global::EngineeringDiscovery.Core.Domain.Activity.EngineeringDecision> Decisions { get; set; }

        // Strongly-typed worker engagements
        public List<WorkerEngagement> WorkerEngagements { get; set; }

        // Strongly-typed registered workers
        public List<WorkerProfile> RegisteredWorkers { get; set; }

        // Strongly-typed handoff state
        public HandoffState? CurrentHandoff { get; set; }

        public ResumePoint? ResumePoint { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
