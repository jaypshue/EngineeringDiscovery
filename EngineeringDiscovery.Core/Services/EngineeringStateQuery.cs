using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Activity;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Read-only projection of the active project's authoritative engineering state.
    /// </summary>
    public sealed class EngineeringStateQuery : IEngineeringStateQuery
    {
        private readonly WorkspaceState _workspaceState;

        public EngineeringStateQuery(WorkspaceState workspaceState)
        {
            _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        }

        private ProjectState? State =>
            _workspaceState.ActiveWorkspace?.ActiveProjectState ??
            _workspaceState.ActiveWorkspace?.ProjectState;

        public ProjectIdentity? GetProjectIdentity() => State?.Identity;

        public ProjectLifecycle? GetLifecycle() => State?.Lifecycle;

        public IReadOnlyList<CompletedCapability> GetCompletedCapabilities()
        {
            var list = State?.CompletedCapabilities;
            return list is null || list.Count == 0 ? Array.Empty<CompletedCapability>() : list.AsReadOnly();
        }

        public IReadOnlyList<KnownIssue> GetOpenIssues()
        {
            var list = State?.KnownIssues;
            if (list is null || list.Count == 0) return Array.Empty<KnownIssue>();
            return list
                .Where(issue => issue.Status is IssueStatus.Open or IssueStatus.InProgress)
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<EngineeringDecision> GetDecisions()
        {
            var list = State?.Decisions;
            return list is null || list.Count == 0 ? Array.Empty<EngineeringDecision>() : list.AsReadOnly();
        }

        public IReadOnlyList<WorkerEngagement> GetRecentEngagements(int count)
        {
            if (count <= 0) return Array.Empty<WorkerEngagement>();
            var list = State?.WorkerEngagements;
            if (list is null || list.Count == 0) return Array.Empty<WorkerEngagement>();
            return list
                .OrderByDescending(engagement => engagement.StartedUtc)
                .Take(count)
                .ToList()
                .AsReadOnly();
        }

        public ResumePoint? GetResumePoint() => State?.ResumePoint;

        public HandoffState? GetCurrentHandoff() => State?.CurrentHandoff;

        public WorkspaceContext? GetWorkspaceContext()
        {
            var ws = _workspaceState.ActiveWorkspace;
            if (ws == null) return null;

            var activeProject = ws.ActiveProject;
            var linkedRepository = activeProject is null
                ? null
                : ws.FindImportedRepositoryForProject(activeProject.Id);

            var repositoryPath = linkedRepository?.RepositoryPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(repositoryPath) && activeProject?.RepositoryPaths is not null)
            {
                repositoryPath = activeProject.RepositoryPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                repositoryPath = ws.RepositoryPath ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(repositoryPath) && ws.ImportedRepositories is not null)
            {
                repositoryPath = ws.ImportedRepositories.FirstOrDefault()?.RepositoryPath ?? string.Empty;
            }

            var investigation = linkedRepository?.Investigation ?? ws.Investigation;
            if (investigation is null && !string.IsNullOrWhiteSpace(repositoryPath))
            {
                investigation = ws.ImportedRepositories?
                    .FirstOrDefault(repository => Workspace.PathsEqual(repository.RepositoryPath, repositoryPath))
                    ?.Investigation;
            }
            investigation ??= ws.ImportedRepositories?
                .FirstOrDefault(repository => repository.Investigation is not null)
                ?.Investigation;
            var repositoryName = string.Empty;
            if (!string.IsNullOrWhiteSpace(repositoryPath))
            {
                try { repositoryName = System.IO.Path.GetFileName(repositoryPath.TrimEnd('\\', '/')); }
                catch { repositoryName = repositoryPath; }
            }

            return new WorkspaceContext
            {
                HasRepository = !string.IsNullOrWhiteSpace(repositoryPath),
                RepositoryName = repositoryName,
                RepositoryPath = repositoryPath,
                HasInvestigation = investigation is not null,
                DiscoveredTypeCount = investigation?.TypeObservations?.Count ?? 0,
                DiscoveredNamespaceCount = investigation?.NamespaceObservations?.Count ?? 0,
                DiscoveredMemberCount = investigation?.MemberObservations?.Count ?? 0
            };
        }

        public EngineeringDiscovery.Core.Domain.Investigation.Investigation? GetActiveInvestigation()
        {
            var ws = _workspaceState.ActiveWorkspace;
            if (ws is null) return null;

            var activeProject = ws.ActiveProject;
            var linkedRepository = activeProject is null
                ? null
                : ws.FindImportedRepositoryForProject(activeProject.Id);
            if (linkedRepository?.Investigation is not null)
            {
                return linkedRepository.Investigation;
            }

            if (ws.Investigation is not null)
            {
                return ws.Investigation;
            }

            if (activeProject?.RepositoryPaths is not null)
            {
                var activePath = activeProject.RepositoryPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
                if (!string.IsNullOrWhiteSpace(activePath))
                {
                    var activeRepository = ws.ImportedRepositories?
                        .FirstOrDefault(repository => Workspace.PathsEqual(repository.RepositoryPath, activePath));
                    if (activeRepository?.Investigation is not null)
                    {
                        return activeRepository.Investigation;
                    }
                }
            }

            return ws.ImportedRepositories?
                .FirstOrDefault(repository => repository.Investigation is not null)
                ?.Investigation;
        }

        public string GenerateStatusSummary()
        {
            var state = State;
            if (state is null)
            {
                return "No project state established. Use EngineOS Chat to identify your project and begin engineering work.";
            }

            var parts = new List<string>();
            if (state.Identity is not null && !string.IsNullOrWhiteSpace(state.Identity.Name))
            {
                parts.Add($"Project: {state.Identity.Name}");
                if (!string.IsNullOrWhiteSpace(state.Identity.Description)) parts.Add($"Description: {state.Identity.Description}");
                if (!string.IsNullOrWhiteSpace(state.Identity.ProductVision)) parts.Add($"Vision: {state.Identity.ProductVision}");
            }
            else
            {
                parts.Add("Project: (unnamed)");
            }

            if (state.Lifecycle is not null)
            {
                parts.Add($"Phase: {state.Lifecycle.Phase}");
                if (!string.IsNullOrWhiteSpace(state.Lifecycle.CurrentFocus)) parts.Add($"Focus: {state.Lifecycle.CurrentFocus}");
            }

            var workspaceContext = GetWorkspaceContext();
            if (workspaceContext is not null && (workspaceContext.HasRepository || workspaceContext.HasInvestigation))
            {
                var repository = workspaceContext.HasRepository ? workspaceContext.RepositoryName : "none";
                var investigation = workspaceContext.HasInvestigation
                    ? $"{workspaceContext.DiscoveredTypeCount} types, {workspaceContext.DiscoveredNamespaceCount} namespaces, {workspaceContext.DiscoveredMemberCount} members"
                    : "not available";
                parts.Add($"Repository: {repository} | Investigation: {investigation}");
            }

            var capabilities = state.CompletedCapabilities ?? new List<CompletedCapability>();
            var acceptedCapabilities = capabilities.Count(capability => capability.Acceptance == CapabilityAcceptance.Accepted);
            var openIssues = GetOpenIssues();
            var engagements = state.WorkerEngagements ?? new List<WorkerEngagement>();
            parts.Add($"Capabilities: {capabilities.Count} ({acceptedCapabilities} accepted), Open issues: {openIssues.Count}, Engagements: {engagements.Count}");

            if (state.CurrentHandoff is not null)
            {
                parts.Add($"Handoff: ready for {state.CurrentHandoff.Objective}");
            }
            else
            {
                parts.Add("Handoff: none");
            }

            if (state.ResumePoint is not null)
            {
                if (!string.IsNullOrWhiteSpace(state.ResumePoint.Summary)) parts.Add($"Resume: {state.ResumePoint.Summary}");
                if (!string.IsNullOrWhiteSpace(state.ResumePoint.NextRecommendedAction)) parts.Add($"Next action: {state.ResumePoint.NextRecommendedAction}");
            }

            return string.Join(" | ", parts);
        }
    }
}
