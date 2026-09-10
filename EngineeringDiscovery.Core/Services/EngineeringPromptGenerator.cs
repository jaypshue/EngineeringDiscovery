using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Activity;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Generates worker-neutral engineering prompts from project state.
    /// The output should be directly usable by any coding agent or human developer.
    /// </summary>
    public static class EngineeringPromptGenerator
    {
        /// <summary>
        /// Generates a complete implementation-ready engineering prompt from a HandoffState and ProjectState context.
        /// </summary>
        public static string GeneratePrompt(HandoffState handoff, ProjectState? projectState)
        {
            if (handoff == null) throw new ArgumentNullException(nameof(handoff));

            var sb = new StringBuilder();

            // Title
            sb.AppendLine($"# {handoff.Objective}");
            sb.AppendLine();

            // Project context — orient the worker
            if (projectState?.Identity != null)
            {
                sb.AppendLine("## Project Context");
                sb.AppendLine();
                sb.AppendLine($"**Project:** {projectState.Identity.Name}");
                if (!string.IsNullOrWhiteSpace(projectState.Identity.Description))
                    sb.AppendLine($"**Description:** {projectState.Identity.Description}");
                if (!string.IsNullOrWhiteSpace(projectState.Identity.ProductVision))
                    sb.AppendLine($"**Vision:** {projectState.Identity.ProductVision}");
                if (projectState.Lifecycle != null)
                {
                    sb.AppendLine($"**Phase:** {projectState.Lifecycle.Phase}");
                    if (!string.IsNullOrWhiteSpace(projectState.Lifecycle.CurrentFocus))
                        sb.AppendLine($"**Current Focus:** {projectState.Lifecycle.CurrentFocus}");
                }
                sb.AppendLine();
            }

            // Goal — what this task is trying to accomplish
            sb.AppendLine("## Goal");
            sb.AppendLine();
            sb.AppendLine(handoff.Objective);
            if (!string.IsNullOrWhiteSpace(handoff.Context))
            {
                sb.AppendLine();
                sb.AppendLine(handoff.Context);
            }
            sb.AppendLine();

            // Previous work — what has been attempted before
            if (!string.IsNullOrWhiteSpace(handoff.PreviousEngagementSummary))
            {
                sb.AppendLine("## Previous Work");
                sb.AppendLine();
                sb.AppendLine(handoff.PreviousEngagementSummary);
                sb.AppendLine();
            }

            // Engineering decisions that constrain this work
            if (projectState?.Decisions != null && projectState.Decisions.Count > 0)
            {
                var accepted = projectState.Decisions
                    .Where(d => d.Status == DecisionStatus.Accepted)
                    .OrderByDescending(d => d.CreatedUtc)
                    .Take(5)
                    .ToList();

                if (accepted.Count > 0)
                {
                    sb.AppendLine("## Engineering Decisions");
                    sb.AppendLine();
                    sb.AppendLine("These accepted decisions constrain the implementation:");
                    sb.AppendLine();
                    foreach (var d in accepted)
                    {
                        sb.AppendLine($"- {d.Statement}");
                    }
                    sb.AppendLine();
                }
            }

            // Known issues relevant to this work
            if (projectState?.KnownIssues != null && projectState.KnownIssues.Count > 0)
            {
                sb.AppendLine("## Known Issues");
                sb.AppendLine();
                sb.AppendLine("Be aware of these existing issues:");
                sb.AppendLine();
                foreach (var issue in projectState.KnownIssues.Take(5))
                {
                    var description = string.IsNullOrWhiteSpace(issue.Description) ? string.Empty : $": {issue.Description}";
                    sb.AppendLine($"- [{issue.Severity}] {issue.Title}{description}");
                }
                sb.AppendLine();
            }

            // Relevant files
            if (handoff.RelevantFiles != null && handoff.RelevantFiles.Count > 0)
            {
                sb.AppendLine("## Relevant Files");
                sb.AppendLine();
                foreach (var f in handoff.RelevantFiles)
                {
                    sb.AppendLine($"- `{f}`");
                }
                sb.AppendLine();
            }

            // Constraints
            if (handoff.Constraints != null && handoff.Constraints.Count > 0)
            {
                sb.AppendLine("## Constraints");
                sb.AppendLine();
                foreach (var c in handoff.Constraints)
                {
                    sb.AppendLine($"- {c}");
                }
                sb.AppendLine();
            }

            // Acceptance criteria — how we know this is done
            if (!string.IsNullOrWhiteSpace(handoff.AcceptanceCriteria))
            {
                sb.AppendLine("## Acceptance Criteria");
                sb.AppendLine();
                sb.AppendLine(handoff.AcceptanceCriteria);
                sb.AppendLine();
            }

            // Scope boundary
            sb.AppendLine("## Scope");
            sb.AppendLine();
            sb.AppendLine("- Implement only what is described above.");
            sb.AppendLine("- Do not redesign unrelated architecture.");
            sb.AppendLine("- Do not introduce new frameworks or dependencies unless explicitly required by the objective.");
            sb.AppendLine();

            // Verification
            sb.AppendLine("## Verification");
            sb.AppendLine();
            sb.AppendLine("After implementation:");
            sb.AppendLine("- Build the affected project(s) — must compile clean.");
            sb.AppendLine("- Run existing tests — all must pass.");
            sb.AppendLine("- Add/update tests for new behavior.");
            sb.AppendLine("- Report: files changed, build result, test result.");
            sb.AppendLine();

            return sb.ToString().TrimEnd() + "\n";
        }

        /// <summary>
        /// Determines the next engineering action from current project state and assembles a HandoffState for it.
        /// </summary>
        public static (string recommendation, string rationale, HandoffState? handoff) DetermineNextAction(ProjectState? projectState)
        {
            if (projectState == null)
            {
                return ("Establish project identity.",
                        "No project state exists. Begin by describing the project in conversation so EngineOS can maintain durable engineering context.",
                        null);
            }

            if (projectState.Identity == null || string.IsNullOrWhiteSpace(projectState.Identity.Name) || projectState.Identity.Name == "Unnamed")
            {
                return ("Establish project identity.",
                        "Project state exists but identity has not been established. Describe the project so EngineOS knows what it is working on.",
                        null);
            }

            if (projectState.Lifecycle == null || projectState.Lifecycle.Phase == LifecyclePhase.Inception)
            {
                return ("Set the project lifecycle phase and engineering focus.",
                        "Project identity is established but the lifecycle phase has not been set. Tell EngineOS what development phase the project is in and what the current engineering focus is.",
                        null);
            }

            var focus = projectState.Lifecycle.CurrentFocus;
            var phase = projectState.Lifecycle.Phase;
            var identity = projectState.Identity;

            // Priority 1: Failed/partial engagements need retry
            var recentFailed = projectState.WorkerEngagements
                .Where(e => e.Outcome == EngagementOutcome.Failed || e.Outcome == EngagementOutcome.PartiallyCompleted)
                .OrderByDescending(e => e.StartedUtc)
                .FirstOrDefault();

            if (recentFailed != null)
            {
                var failedHandoff = AssembleRetryHandoff(recentFailed, identity, focus);
                return ($"Complete previously failed work: {recentFailed.TaskDescription}",
                        $"A previous worker ({recentFailed.WorkerName}) attempted \"{recentFailed.TaskDescription}\" but the outcome was {recentFailed.Outcome}. Summary: {recentFailed.Summary}",
                        failedHandoff);
            }

            // Priority 2: Use ResumePoint if it has a specific recommendation
            var resumeAction = projectState.ResumePoint?.NextRecommendedAction;
            var resumeRationale = projectState.ResumePoint?.Rationale;

            // Priority 3: Derive from lifecycle + focus
            var handoff = AssembleNextActionHandoff(phase, focus, identity, projectState, resumeAction);
            var objective = handoff.Objective;
            var rationale = !string.IsNullOrWhiteSpace(resumeRationale)
                ? resumeRationale
                : $"Project is in {phase}. Current focus: {focus}. This is the logical next engineering step.";

            return (objective, rationale, handoff);
        }

        /// <summary>
        /// Generates a next-action determination from an explicit user-specified focus.
        /// Used when the human says "the next task is X" or "let's work on X next."
        /// </summary>
        public static (string recommendation, string rationale, HandoffState handoff) DetermineActionForFocus(
            string explicitFocus, ProjectState? projectState)
        {
            var identity = projectState?.Identity;
            var phase = projectState?.Lifecycle?.Phase ?? LifecyclePhase.ActiveDevelopment;

            var handoff = new HandoffState
            {
                Objective = explicitFocus,
                Context = BuildContextString(identity, phase, explicitFocus, projectState),
                AcceptanceCriteria = BuildAcceptanceCriteria(phase, explicitFocus),
                Constraints = BuildConstraints(phase, projectState)
            };

            // Populate relevant files from recent engagements if related
            PopulateRelevantFiles(handoff, projectState);

            var rationale = $"The human specified this as the next engineering objective.";
            return (explicitFocus, rationale, handoff);
        }

        // ─── Private helpers ────────────────────────────────────────────────────────

        private static HandoffState AssembleRetryHandoff(WorkerEngagement failed, ProjectIdentity identity, string focus)
        {
            var handoff = new HandoffState
            {
                Objective = $"Complete: {failed.TaskDescription}",
                Context = $"Project: {identity.Name}. A previous worker ({failed.WorkerName}) attempted this task but the outcome was: {failed.Outcome}.\n\nPrevious attempt summary: {failed.Summary}\n\nThis work needs to be completed, potentially with a different approach.",
                PreviousEngagementId = failed.Id,
                PreviousEngagementSummary = $"Worker: {failed.WorkerName}. Outcome: {failed.Outcome}. {failed.Summary}",
                AcceptanceCriteria = $"The originally intended work (\"{failed.TaskDescription}\") is complete.\n- Build succeeds.\n- Existing tests pass.\n- New behavior is tested.\n- Report what approach was taken and how it differs from the previous attempt.",
                Constraints = new List<string>
                {
                    "Do not repeat the same approach if it previously failed — try a different strategy.",
                    "Preserve existing functionality that currently works.",
                    "Keep changes focused on completing the specific failed task."
                }
            };

            if (failed.FilesChanged.Count > 0)
                handoff.RelevantFiles = new List<string>(failed.FilesChanged);

            return handoff;
        }

        private static HandoffState AssembleNextActionHandoff(
            LifecyclePhase phase, string focus, ProjectIdentity identity,
            ProjectState projectState, string? resumeAction)
        {
            var objective = !string.IsNullOrWhiteSpace(resumeAction) && resumeAction.Length > 15
                ? resumeAction
                : (!string.IsNullOrWhiteSpace(focus) ? focus : $"Continue development on {identity.Name}");

            var handoff = new HandoffState
            {
                Objective = objective,
                Context = BuildContextString(identity, phase, focus, projectState),
                AcceptanceCriteria = BuildAcceptanceCriteria(phase, focus),
                Constraints = BuildConstraints(phase, projectState)
            };

            PopulateRelevantFiles(handoff, projectState);

            return handoff;
        }

        private static string BuildContextString(ProjectIdentity? identity, LifecyclePhase phase, string focus, ProjectState? projectState)
        {
            var sb = new StringBuilder();

            if (identity != null)
            {
                sb.AppendLine($"{identity.Name} is in {phase} phase.");
                if (!string.IsNullOrWhiteSpace(identity.ProductVision))
                    sb.AppendLine($"Product vision: {identity.ProductVision}");
                if (!string.IsNullOrWhiteSpace(focus))
                    sb.AppendLine($"Current engineering focus: {focus}");
            }

            // Include recent completed work so the worker knows what's already done
            if (projectState?.WorkerEngagements != null)
            {
                var recentCompleted = projectState.WorkerEngagements
                    .Where(e => e.Outcome == EngagementOutcome.Completed && e.Acceptance == AcceptanceStatus.Accepted)
                    .OrderByDescending(e => e.CompletedUtc ?? e.StartedUtc)
                    .Take(3)
                    .ToList();

                if (recentCompleted.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Recently completed work (do not repeat):");
                    foreach (var e in recentCompleted)
                    {
                        sb.AppendLine($"- {e.TaskDescription}");
                    }
                }
            }

            return sb.ToString().Trim();
        }

        private static string BuildAcceptanceCriteria(LifecyclePhase phase, string focus)
        {
            var sb = new StringBuilder();

            sb.AppendLine("The implementation is complete when:");
            sb.AppendLine($"- The objective (\"{focus}\") is demonstrably addressed.");
            sb.AppendLine("- The solution builds with zero errors.");
            sb.AppendLine("- All existing tests continue to pass.");
            sb.AppendLine("- New/changed behavior has corresponding test coverage.");
            sb.AppendLine("- No unrelated functionality is broken.");

            if (phase == LifecyclePhase.Stabilization)
            {
                sb.AppendLine("- Edge cases are identified and handled.");
                sb.AppendLine("- No new features are introduced beyond what is required.");
            }

            return sb.ToString().Trim();
        }

        private static List<string> BuildConstraints(LifecyclePhase phase, ProjectState? projectState)
        {
            var constraints = new List<string>();

            switch (phase)
            {
                case LifecyclePhase.ActiveDevelopment:
                    constraints.Add("Preserve existing architecture and patterns.");
                    constraints.Add("Do not introduce new frameworks or major dependencies without justification.");
                    constraints.Add("Keep changes incremental — prefer a working smaller change over a large incomplete one.");
                    break;
                case LifecyclePhase.Stabilization:
                    constraints.Add("Do not add new features beyond what the objective requires.");
                    constraints.Add("Prioritize correctness, test coverage, and edge-case handling.");
                    constraints.Add("Fix existing issues before introducing complexity.");
                    break;
                case LifecyclePhase.Maintenance:
                    constraints.Add("Minimal changes only — do not refactor beyond what is needed.");
                    constraints.Add("Ensure strict backward compatibility.");
                    constraints.Add("Document any behavioral changes.");
                    break;
                default:
                    constraints.Add("Keep changes focused and testable.");
                    break;
            }

            // Add constraints from accepted decisions
            if (projectState?.Decisions != null)
            {
                var constrainingDecisions = projectState.Decisions
                    .Where(d => d.Status == DecisionStatus.Accepted)
                    .OrderByDescending(d => d.CreatedUtc)
                    .Take(3)
                    .Select(d => d.Statement)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                foreach (var d in constrainingDecisions)
                {
                    constraints.Add($"Decision: {d}");
                }
            }

            return constraints;
        }

        private static void PopulateRelevantFiles(HandoffState handoff, ProjectState? projectState)
        {
            if (projectState == null) return;

            // Use files from recent engagements as hints for relevant areas
            var recentFiles = projectState.WorkerEngagements
                .Where(e => e.FilesChanged.Count > 0)
                .OrderByDescending(e => e.StartedUtc)
                .Take(3)
                .SelectMany(e => e.FilesChanged)
                .Distinct()
                .Take(10)
                .ToList();

            if (recentFiles.Count > 0)
            {
                handoff.RelevantFiles = recentFiles;
            }
        }
    }
}
