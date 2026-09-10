using System;
using EngineeringDiscovery.Core.Domain.Activity;
using EngineeringDiscovery.Core.Domain.ProjectState;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Domain service that manages mutations to ProjectState.
    /// Every mutation must: update LastUpdatedUtc, call WorkspaceState.Save(),
    /// and fire NotifyStateChanged(). No mutation path may skip persistence.
    /// </summary>
    public interface IProjectStateService
    {
        /// <summary>
        /// Establishes or updates the project identity. Initializes ProjectState if null.
        /// </summary>
        void SetProjectIdentity(ProjectIdentity identity);

        /// <summary>
        /// Updates the lifecycle phase and current focus. Triggers ResumePoint refresh.
        /// </summary>
        void UpdateLifecyclePhase(LifecyclePhase phase, string focus);

        /// <summary>
        /// Records a verified completed capability. Triggers ResumePoint refresh.
        /// </summary>
        void RecordCompletedCapability(CompletedCapability capability);

        /// <summary>
        /// Records a known issue. Triggers ResumePoint refresh.
        /// </summary>
        void RecordKnownIssue(KnownIssue issue);

        /// <summary>
        /// Resolves an existing known issue by ID.
        /// Slice 2 — may throw NotImplementedException until implemented.
        /// </summary>
        void ResolveIssue(Guid issueId, string resolution);

        /// <summary>
        /// Records a worker engagement (start of work by a coding agent).
        /// </summary>
        void RecordWorkerEngagement(WorkerEngagement engagement);

        /// <summary>
        /// Updates an existing engagement's outcome and acceptance status.
        /// Triggers ResumePoint refresh.
        /// </summary>
        void UpdateEngagementOutcome(Guid engagementId, EngagementOutcome outcome, AcceptanceStatus acceptance);

        /// <summary>
        /// Assembles a handoff context package for the next worker.
        /// </summary>
        void AssembleHandoff(HandoffState handoff);

        /// <summary>
        /// Records an accepted engineering decision.
        /// </summary>
        void RecordDecision(EngineeringDecision decision);

        /// <summary>
        /// Clears the current handoff when the user explicitly withdraws it.
        /// </summary>
        void ClearCurrentHandoff();

        /// <summary>
        /// Refreshes the ResumePoint to reflect the current project state.
        /// Called automatically after significant mutations.
        /// </summary>
        void RefreshResumePoint();

        /// <summary>
        /// Returns the current ProjectState, or null if not yet initialized.
        /// </summary>
        ProjectState? GetCurrentState();
    }
}
