using System;
using EngineeringDiscovery.Core.Domain.Activity;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Domain service managing the active project's durable engineering state.
    /// Project.State is authoritative; Workspace.ProjectState is retained only as a
    /// compatibility field for legacy workspaces.
    /// </summary>
    public sealed class ProjectStateService : IProjectStateService
    {
        private readonly WorkspaceState _workspaceState;

        public ProjectStateService(WorkspaceState workspaceState)
        {
            _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        }

        public void SetProjectIdentity(ProjectIdentity identity)
        {
            if (identity is null) throw new ArgumentNullException(nameof(identity));

            var ps = EnsureProjectState();
            var now = DateTime.UtcNow;
            ps.Identity = identity;
            identity.LastUpdatedUtc = now;
            if (identity.EstablishedUtc == default) identity.EstablishedUtc = now;
            ps.LastUpdatedUtc = now;
            Persist();
        }

        public void UpdateLifecyclePhase(LifecyclePhase phase, string focus)
        {
            var ps = EnsureProjectState();
            var now = DateTime.UtcNow;
            ps.Lifecycle ??= new ProjectLifecycle();
            ps.Lifecycle.Phase = phase;
            ps.Lifecycle.CurrentFocus = focus ?? string.Empty;
            ps.Lifecycle.PhaseEnteredUtc = now;
            ps.Lifecycle.LastActivityUtc = now;
            RefreshResumePointInternal(ps, now);
            ps.LastUpdatedUtc = now;
            Persist();
        }

        public void RecordCompletedCapability(CompletedCapability capability)
        {
            if (capability is null) throw new ArgumentNullException(nameof(capability));

            var ps = EnsureProjectState();
            EnsureCollections(ps);
            if (capability.Id == Guid.Empty) capability.Id = Guid.NewGuid();
            if (ContainsCapability(ps, capability.Id)) return;

            if (capability.CompletedUtc == default) capability.CompletedUtc = DateTime.UtcNow;
            ps.CompletedCapabilities.Add(capability);
            TouchAndRefresh(ps);
            Persist();
        }

        public void RecordKnownIssue(KnownIssue issue)
        {
            if (issue is null) throw new ArgumentNullException(nameof(issue));

            var ps = EnsureProjectState();
            EnsureCollections(ps);
            if (issue.Id == Guid.Empty) issue.Id = Guid.NewGuid();
            if (ContainsIssue(ps, issue.Id)) return;

            if (issue.ReportedUtc == default) issue.ReportedUtc = DateTime.UtcNow;
            ps.KnownIssues.Add(issue);
            TouchAndRefresh(ps);
            Persist();
        }

        public void ResolveIssue(Guid issueId, string resolution)
        {
            var ps = EnsureProjectState();
            EnsureCollections(ps);
            var issue = FindIssue(ps, issueId);
            if (issue is null) throw new InvalidOperationException($"Known issue '{issueId}' was not found.");

            // KnownIssue intentionally has no free-form resolution field in the existing
            // specification. Status and ResolvedUtc are the durable resolution boundary.
            _ = resolution;
            if (issue.Status == IssueStatus.Resolved && issue.ResolvedUtc.HasValue) return;

            issue.Status = IssueStatus.Resolved;
            issue.ResolvedUtc = DateTime.UtcNow;
            TouchAndRefresh(ps);
            Persist();
        }

        public void RecordWorkerEngagement(WorkerEngagement engagement)
        {
            if (engagement is null) throw new ArgumentNullException(nameof(engagement));

            var ps = EnsureProjectState();
            EnsureCollections(ps);
            if (engagement.Id == Guid.Empty) engagement.Id = Guid.NewGuid();
            if (FindEngagement(ps, engagement.Id) is not null) return;

            engagement.Outcome = EngagementOutcome.InProgress;
            engagement.Acceptance = AcceptanceStatus.Pending;
            if (engagement.StartedUtc == default) engagement.StartedUtc = DateTime.UtcNow;
            ps.WorkerEngagements.Add(engagement);
            TouchAndRefresh(ps);
            Persist();
        }

        public void UpdateEngagementOutcome(Guid engagementId, EngagementOutcome outcome, AcceptanceStatus acceptance)
        {
            var ps = EnsureProjectState();
            EnsureCollections(ps);
            var engagement = FindEngagement(ps, engagementId);
            if (engagement is null) throw new InvalidOperationException($"Worker engagement '{engagementId}' was not found.");

            engagement.Outcome = outcome;
            engagement.Acceptance = acceptance;
            if (outcome == EngagementOutcome.InProgress)
            {
                engagement.CompletedUtc = null;
            }
            else if (!engagement.CompletedUtc.HasValue)
            {
                engagement.CompletedUtc = DateTime.UtcNow;
            }

            TouchAndRefresh(ps);
            Persist();
        }

        public void AssembleHandoff(HandoffState handoff)
        {
            if (handoff is null) throw new ArgumentNullException(nameof(handoff));

            var ps = EnsureProjectState();
            if (handoff.Id == Guid.Empty) handoff.Id = Guid.NewGuid();
            if (handoff.AssembledUtc == default) handoff.AssembledUtc = DateTime.UtcNow;
            ps.CurrentHandoff = handoff;
            TouchAndRefresh(ps);
            Persist();
        }

        public void RecordDecision(EngineeringDecision decision)
        {
            if (decision is null) throw new ArgumentNullException(nameof(decision));

            var ps = EnsureProjectState();
            EnsureCollections(ps);
            if (decision.Id == Guid.Empty) decision.Id = Guid.NewGuid();
            if (ps.Decisions.Any(existing => existing.Id == decision.Id)) return;

            if (decision.CreatedUtc == default) decision.CreatedUtc = DateTime.UtcNow;
            ps.Decisions.Add(decision);
            TouchAndRefresh(ps);
            Persist();
        }

        public void ClearCurrentHandoff()
        {
            var ps = CurrentState;
            if (ps is null || ps.CurrentHandoff is null) return;

            ps.CurrentHandoff = null;
            TouchAndRefresh(ps);
            Persist();
        }

        public void RefreshResumePoint()
        {
            var ps = CurrentState;
            if (ps is null) return;

            RefreshResumePointInternal(ps, DateTime.UtcNow);
            ps.LastUpdatedUtc = DateTime.UtcNow;
            Persist();
        }

        public ProjectState? GetCurrentState() => CurrentState;

        private ProjectState? CurrentState =>
            _workspaceState.ActiveWorkspace?.ActiveProjectState ??
            _workspaceState.ActiveWorkspace?.ProjectState;

        private ProjectState EnsureProjectState()
        {
            if (_workspaceState.ActiveWorkspace is null)
            {
                _workspaceState.ReplaceWorkspace(new Workspace());
            }

            var workspace = _workspaceState.ActiveWorkspace!;
            if (workspace.ActiveProject is null)
            {
                var project = new Project();
                workspace.Projects ??= new System.Collections.Generic.List<Project>();
                workspace.Projects.Add(project);
                workspace.ActiveProjectId = project.Id;
                workspace.ProjectState = project.State;
            }

            var state = workspace.ActiveProjectState ?? workspace.ProjectState;
            if (state is null)
            {
                state = new ProjectState();
                workspace.ActiveProject!.State = state;
                workspace.ProjectState ??= state;
            }

            EnsureCollections(state);
            return state;
        }

        private static void EnsureCollections(ProjectState state)
        {
            state.CompletedCapabilities ??= new System.Collections.Generic.List<CompletedCapability>();
            state.KnownIssues ??= new System.Collections.Generic.List<KnownIssue>();
            state.WorkerEngagements ??= new System.Collections.Generic.List<WorkerEngagement>();
            state.RegisteredWorkers ??= new System.Collections.Generic.List<WorkerProfile>();
            state.Decisions ??= new System.Collections.Generic.List<global::EngineeringDiscovery.Core.Domain.Activity.EngineeringDecision>();
        }

        private void Persist() => _workspaceState.PersistAndNotify();

        private void TouchAndRefresh(ProjectState state)
        {
            var now = DateTime.UtcNow;
            RefreshResumePointInternal(state, now);
            state.LastUpdatedUtc = now;
        }

        private static bool ContainsCapability(ProjectState state, Guid id)
        {
            foreach (var capability in state.CompletedCapabilities)
            {
                if (capability.Id == id) return true;
            }
            return false;
        }

        private static bool ContainsIssue(ProjectState state, Guid id)
        {
            foreach (var issue in state.KnownIssues)
            {
                if (issue.Id == id) return true;
            }
            return false;
        }

        private static WorkerEngagement? FindEngagement(ProjectState state, Guid id)
        {
            foreach (var engagement in state.WorkerEngagements)
            {
                if (engagement.Id == id) return engagement;
            }
            return null;
        }

        private static KnownIssue? FindIssue(ProjectState state, Guid id)
        {
            foreach (var issue in state.KnownIssues)
            {
                if (issue.Id == id) return issue;
            }
            return null;
        }

        private static void RefreshResumePointInternal(ProjectState state, DateTime now)
        {
            var identityName = string.IsNullOrWhiteSpace(state.Identity?.Name) ? "Unnamed" : state.Identity!.Name;
            var phase = state.Lifecycle?.Phase.ToString() ?? "Unknown";
            var focus = state.Lifecycle?.CurrentFocus ?? string.Empty;
            var openIssueCount = 0;
            var criticalIssue = default(KnownIssue);
            foreach (var issue in state.KnownIssues ?? new System.Collections.Generic.List<KnownIssue>())
            {
                if (issue.Status is IssueStatus.Open or IssueStatus.InProgress)
                {
                    openIssueCount++;
                    if (criticalIssue is null || (int)issue.Severity > (int)criticalIssue.Severity) criticalIssue = issue;
                }
            }

            var failedEngagement = default(WorkerEngagement);
            for (var index = (state.WorkerEngagements?.Count ?? 0) - 1; index >= 0; index--)
            {
                var engagement = state.WorkerEngagements![index];
                if (engagement.Outcome is EngagementOutcome.Failed or EngagementOutcome.Escalated or EngagementOutcome.PartiallyCompleted)
                {
                    failedEngagement = engagement;
                    break;
                }
            }

            string nextAction;
            string rationale;
            if (criticalIssue is not null && criticalIssue.Severity == IssueSeverity.Critical)
            {
                nextAction = $"Address critical issue: {criticalIssue.Title}";
                rationale = "A critical open issue takes precedence over normal project work.";
            }
            else if (state.CurrentHandoff is not null)
            {
                nextAction = $"Execute the current handoff: {state.CurrentHandoff.Objective}";
                rationale = "A durable handoff is ready for the next engineering worker.";
            }
            else if (failedEngagement is not null)
            {
                nextAction = $"Review the {failedEngagement.Outcome.ToString().ToLowerInvariant()} engagement before retrying: {failedEngagement.TaskDescription}";
                rationale = "The most recent relevant worker engagement did not complete cleanly.";
            }
            else
            {
                nextAction = state.Lifecycle?.Phase switch
                {
                    LifecyclePhase.Inception => "Establish project identity and begin initial discovery.",
                    LifecyclePhase.ActiveDevelopment => string.IsNullOrWhiteSpace(focus) ? "Continue active development." : $"Continue active development on: {focus}",
                    LifecyclePhase.Stabilization => string.IsNullOrWhiteSpace(focus) ? "Stabilize and verify the project." : $"Stabilize and verify: {focus}",
                    LifecyclePhase.Maintenance => string.IsNullOrWhiteSpace(focus) ? "Maintain and monitor the project." : $"Maintain and monitor: {focus}",
                    LifecyclePhase.Paused => "Review project status before resuming work.",
                    LifecyclePhase.Archived => "Project is archived. No action recommended.",
                    _ => "Establish project identity and begin initial discovery."
                };
                rationale = $"Based on current lifecycle phase ({phase}) and project focus.";
            }

            var focusSummary = string.IsNullOrWhiteSpace(focus) ? string.Empty : $", Focus: {focus}";
            var handoffSummary = state.CurrentHandoff is null ? "none" : "ready";
            state.ResumePoint = new ResumePoint
            {
                CapturedUtc = now,
                Summary = $"Project: {identityName}, Phase: {phase}{focusSummary}. Capabilities: {state.CompletedCapabilities?.Count ?? 0}, Open issues: {openIssueCount}, Engagements: {state.WorkerEngagements?.Count ?? 0}, Handoff: {handoffSummary}.",
                NextRecommendedAction = nextAction,
                Rationale = rationale,
                BlockingIssueIds = criticalIssue is null ? new System.Collections.Generic.List<Guid>() : new System.Collections.Generic.List<Guid> { criticalIssue.Id },
                ActiveWorkItemId = state.CurrentHandoff?.PreviousEngagementId
            };
        }
    }
}
