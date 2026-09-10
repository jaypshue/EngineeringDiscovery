using System;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Activity;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Provides read-only query access to the authoritative engineering state.
    /// Used by the conversation layer to answer questions about project status.
    /// All methods are null-safe: when ProjectState is null (legacy workspace),
    /// reference-type methods return null, collection methods return empty lists,
    /// and GenerateStatusSummary returns a descriptive non-empty string.
    /// </summary>
    public interface IEngineeringStateQuery
    {
        /// <summary>
        /// Returns the project identity, or null when no project state exists.
        /// </summary>
        ProjectIdentity? GetProjectIdentity();

        /// <summary>
        /// Returns the project lifecycle, or null when no project state exists.
        /// </summary>
        ProjectLifecycle? GetLifecycle();

        /// <summary>
        /// Returns completed capabilities. Returns empty list when no project state exists.
        /// </summary>
        IReadOnlyList<CompletedCapability> GetCompletedCapabilities();

        /// <summary>
        /// Returns open (unresolved) known issues. Returns empty list when no project state exists.
        /// </summary>
        IReadOnlyList<KnownIssue> GetOpenIssues();

        /// <summary>
        /// Returns project-level engineering decisions. Returns empty list when no project state exists.
        /// </summary>
        IReadOnlyList<EngineeringDecision> GetDecisions();

        /// <summary>
        /// Returns the most recent worker engagements (up to count).
        /// Returns empty list when no project state exists.
        /// </summary>
        IReadOnlyList<WorkerEngagement> GetRecentEngagements(int count);

        /// <summary>
        /// Returns the current resume point, or null when no project state exists.
        /// </summary>
        ResumePoint? GetResumePoint();

        /// <summary>
        /// Returns the current handoff state, or null when no handoff is assembled.
        /// </summary>
        HandoffState? GetCurrentHandoff();

        /// <summary>
        /// Returns workspace-level context including repository/investigation status.
        /// Returns null when no workspace exists.
        /// </summary>
        WorkspaceContext? GetWorkspaceContext();

        /// <summary>
        /// Generates a human-readable status summary from all available project state.
        /// Always returns a non-null, non-empty string (even when ProjectState is null).
        /// </summary>
        string GenerateStatusSummary();

        /// <summary>
        /// Returns the active workspace investigation, or null when no investigation exists.
        /// Used by EngineeringPartner to compose evidence-based repository summaries.
        /// </summary>
        EngineeringDiscovery.Core.Domain.Investigation.Investigation? GetActiveInvestigation();
    }
}
