using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.CurrentTask;
using System.Text.Json;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Domain.Activity;
using Microsoft.Extensions.Logging;

namespace EngineeringDiscovery.Core.Services
{
    // Note: EngineeringInsight currently lives in Core as a temporary measure during migration.
    // In ED-205 final state, presentation DTOs will be moved to presentation projects.
    public sealed record EngineeringInsight(string Subject, string Observation, string Category);
    // Core-owned WorkspaceState - single source of truth for application state
    public sealed class WorkspaceState
    {
        private readonly IWorkspacePersistence _persistence;
        private readonly ILogger<WorkspaceState>? _logger;
        private readonly IRepoFingerprintService _fingerprintService;

        public WorkspaceState(IWorkspacePersistence persistence, IRepoFingerprintService fingerprintService, ILogger<WorkspaceState>? logger = null)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _fingerprintService = fingerprintService ?? throw new ArgumentNullException(nameof(fingerprintService));
            _logger = logger;

            // Do not perform I/O in constructor per ED-205 rule. Hosts/tests must explicitly load persisted
            // workspace and call ReplaceWorkspace to populate canonical state.
            ActiveWorkspace = null;
        }

        public Workspace? ActiveWorkspace { get; private set; }

        // Convenience accessor for imported repositories collection
        public System.Collections.Generic.List<Domain.Workspace.ImportedRepository>? ImportedRepositories => ActiveWorkspace?.ImportedRepositories;

        // ED-300: expose a convenience accessor for the current activity
        public global::EngineeringDiscovery.Core.Domain.Activity.EngineeringActivity? CurrentActivity => ActiveWorkspace?.CurrentActivity;

        // ED-302: project current hypothesis (read-only projection)
        public EngineeringHypothesis? CurrentHypothesis => CurrentActivity?.CurrentHypothesis;

        // ED-302: convenience access to the hypothesis space for the current activity
        public System.Collections.Generic.List<EngineeringHypothesis>? CurrentHypothesisSpace => CurrentActivity?.HypothesisSpace;

        // ED-303: project current evidence request (read-only)
        public EngineeringEvidenceRequest? CurrentEvidenceRequest => CurrentActivity?.CurrentEvidenceRequest;

        // ED-303: convenience access to the evidence requests for the current activity
        public System.Collections.Generic.List<EngineeringEvidenceRequest>? CurrentEvidenceRequests => CurrentActivity?.EvidenceRequests;

        // ED-304: project evidence collection (read-only)
        public System.Collections.Generic.List<EngineeringEvidence>? CurrentEvidence => CurrentActivity?.Evidence;

        // ED-305: project recovered understanding (read-only)
        public System.Collections.Generic.List<EngineeringRecoveredUnderstanding>? CurrentRecoveredUnderstanding => CurrentActivity?.RecoveredUnderstanding;

        public EngineeringRecoveredUnderstanding? CurrentRecoveredUnderstandingItem => CurrentActivity?.CurrentRecoveredUnderstanding;

        public bool HasWorkspace => ActiveWorkspace is not null && !ActiveWorkspace.IsEmpty();

        public event Action? OnChange;

        // Presentation view state removed per ED-205. Presentation hosts must implement IViewStateStore
        // and manage any UI-only state such as GraphViewState. WorkspaceState no longer stores view state.

        public enum EngineeringModelFreshness
        {
            Unknown,
            Current,
            RefreshRecommended,
            RefreshRequired
        }

        // Determine model freshness by delegating to IRepoFingerprintService
        public EngineeringModelFreshness GetFreshnessStatus()
        {
            try
            {
                if (ActiveWorkspace is null) return EngineeringModelFreshness.Unknown;

                var activeProject = ActiveWorkspace.ActiveProject;
                var linkedRepository = activeProject is null
                    ? null
                    : ActiveWorkspace.FindImportedRepositoryForProject(activeProject.Id);
                var repoPath = linkedRepository?.RepositoryPath;
                if (string.IsNullOrWhiteSpace(repoPath) && activeProject?.RepositoryPaths is not null)
                {
                    repoPath = activeProject.RepositoryPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
                }
                if (string.IsNullOrWhiteSpace(repoPath))
                {
                    repoPath = ActiveWorkspace.RepositoryPath;
                }
                if (string.IsNullOrWhiteSpace(repoPath) && ActiveWorkspace.ImportedRepositories is not null)
                {
                    repoPath = ActiveWorkspace.ImportedRepositories.FirstOrDefault()?.RepositoryPath;
                }
                if (string.IsNullOrWhiteSpace(repoPath)) return EngineeringModelFreshness.Unknown;

                var investigation = linkedRepository?.Investigation ?? ActiveWorkspace.Investigation;
                if (investigation is null && ActiveWorkspace.ImportedRepositories is not null)
                {
                    investigation = ActiveWorkspace.ImportedRepositories
                        .FirstOrDefault(repository => Domain.Workspace.Workspace.PathsEqual(repository.RepositoryPath, repoPath))
                        ?.Investigation;
                }
                if (investigation is null) return EngineeringModelFreshness.Unknown;

                var builtUtc = linkedRepository?.LastBuiltUtc ?? ActiveWorkspace.LastBuiltUtc;
                var fingerprint = linkedRepository?.RepositoryFingerprint ?? ActiveWorkspace.RepositoryFingerprint;

                // Host-provided service evaluates freshness according to configured policy
                var task = _fingerprintService.EvaluateFreshnessAsync(repoPath, builtUtc, fingerprint);
                var result = task.GetAwaiter().GetResult();
                return result switch
                {
                    ModelFreshness.Current => EngineeringModelFreshness.Current,
                    ModelFreshness.RefreshRecommended => EngineeringModelFreshness.RefreshRecommended,
                    ModelFreshness.RefreshRequired => EngineeringModelFreshness.RefreshRequired,
                    _ => EngineeringModelFreshness.Unknown,
                };
            }
            catch
            {
                return EngineeringModelFreshness.Unknown;
            }
        }

        // Persistence delegated to IWorkspacePersistence implementation supplied by host.
        // The boolean result is intentionally observable by workflows that must not present
        // an in-memory-only operation as successful.
        public bool Save()
        {
            try
            {
                _persistence.SaveAsync(ActiveWorkspace).GetAwaiter().GetResult();
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist workspace state.");
                return false;
            }
        }

        /// <summary>
        /// Persists current state and notifies listeners. The notification still occurs
        /// when persistence fails so observers can surface the in-memory state transition;
        /// callers can inspect the returned result when durability is required.
        /// </summary>
        public bool PersistAndNotify()
        {
            var persisted = Save();
            NotifyStateChanged();
            return persisted;
        }

        // Presentation wiring removed from Core in ED-205. Hosts should implement persistence adapters
        // and subscribe to presentation events outside of WorkspaceState.

        private void NotifyStateChanged()
        {
            try
            {
                OnChange?.Invoke();
            }
            catch
            {
                // Swallow observer exceptions to keep host stable.
            }
        }

        // Operations to mutate state - keep minimal; UI should call into domain services in future
        public void ReplaceWorkspace(Workspace workspace)
        {
            // Migration: if persisted workspace still uses legacy RepositoryPath and
            // ImportedRepositories is empty, convert legacy RepositoryPath into an ImportedRepository
            try
            {
                if (workspace != null && (workspace.ImportedRepositories == null || workspace.ImportedRepositories.Count == 0) && !string.IsNullOrWhiteSpace(workspace.RepositoryPath))
                {
                    // Preserve existing Investigation if present on the workspace as the ImportedRepository's investigation
                    var imported = new Domain.Workspace.ImportedRepository
                    {
                        RepositoryPath = workspace.RepositoryPath,
                        Investigation = workspace.Investigation,
                        CreatedUtc = workspace.CreatedUtc,
                        LastBuiltUtc = workspace.LastBuiltUtc,
                        RepositoryFingerprint = workspace.RepositoryFingerprint
                    };
                    workspace.ImportedRepositories = new System.Collections.Generic.List<Domain.Workspace.ImportedRepository> { imported };
                }
            }
            catch
            {
                // Swallow migration failures to avoid preventing workspace activation
            }

            // Migration: if persisted workspace has legacy ProjectState but empty Projects list,
            // create a Project from the legacy state and promote it.
            try
            {
                if (workspace != null && workspace.ProjectState != null && (workspace.Projects == null || workspace.Projects.Count == 0))
                {
                    var legacyProject = new Domain.Workspace.Project
                    {
                        State = workspace.ProjectState,
                        CreatedUtc = workspace.ProjectState.CreatedUtc,
                        LastOpenedUtc = DateTime.UtcNow
                    };

                    // Migrate repository paths from ImportedRepositories to the project
                    if (workspace.ImportedRepositories != null)
                    {
                        foreach (var repo in workspace.ImportedRepositories)
                        {
                            if (!string.IsNullOrWhiteSpace(repo.RepositoryPath))
                            {
                                legacyProject.RepositoryPaths.Add(repo.RepositoryPath);
                            }
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(workspace.RepositoryPath))
                    {
                        legacyProject.RepositoryPaths.Add(workspace.RepositoryPath);
                    }

                    if (workspace.Projects == null) workspace.Projects = new System.Collections.Generic.List<Domain.Workspace.Project>();
                    workspace.Projects.Add(legacyProject);
                    workspace.ActiveProjectId = legacyProject.Id;

                    // Clear legacy field to avoid dual-state (but keep for serialization compat)
                    // workspace.ProjectState = null; // Uncomment once migration is confirmed stable
                }
            }
            catch
            {
                // Swallow migration failures to avoid preventing workspace activation
            }

            // Establish the explicit folder-to-internal-project relationship after legacy
            // repository/project migrations have completed.
            try
            {
                workspace?.EstablishRepositoryProjectLinks();
            }
            catch
            {
                // Keep workspace activation resilient for malformed legacy data.
            }

            ActiveWorkspace = workspace;
            // Do not perform persistence here; persistence is the responsibility of workflow services.
            NotifyStateChanged();
        }

        public void SetInvestigation(Domain.Investigation.Investigation? investigation)
        {
            if (ActiveWorkspace is null) ActiveWorkspace = new Workspace();
            // Store investigation at workspace-level for backward compatibility. Do not overwrite per-repo investigations.
            ActiveWorkspace.Investigation = investigation;
            try
            {
                // Lightweight diagnostic tracing for investigation shape at the state boundary
                var id = investigation is null ? "NULL" : investigation.Id.ToString();
                var typeCount = investigation?.TypeObservations?.Count ?? 0;
                var nsCount = investigation?.NamespaceObservations?.Count ?? 0;
                var memberCount = investigation?.MemberObservations?.Count ?? 0;
                Console.WriteLine($"[INV-DIAG] UTC {DateTime.UtcNow:o} WorkspaceState.SetInvestigation id={id} TypeObservations={typeCount} NamespaceObservations={nsCount} MemberObservations={memberCount}");
            }
            catch
            {
                // Intentionally do not swallow or change behavior; diagnostic best-effort only
            }
            // Persist changes
            Save();
            NotifyStateChanged();
        }

        // Backwards-compatibility helpers removed. Presentation and workflow responsibilities
        // have been migrated to presentation services and ICurrentTaskService respectively.

        // Compute a simple repository fingerprint
        public string? ComputeRepositoryFingerprint(string repositoryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repositoryPath)) return null;
                if (File.Exists(repositoryPath))
                {
                    var fi = new FileInfo(repositoryPath);
                    return fi.LastWriteTimeUtc.ToString("o");
                }

                if (!Directory.Exists(repositoryPath)) return null;

                var topFiles = Directory.EnumerateFiles(repositoryPath, "*.*", SearchOption.TopDirectoryOnly);
                var solFiles = Directory.EnumerateFiles(repositoryPath, "*.sln*", SearchOption.AllDirectories);

                var fileTimes = topFiles.Concat(solFiles).Select(p => File.GetLastWriteTimeUtc(p));
                if (!fileTimes.Any()) return null;
                var latest = fileTimes.Max();
                return latest.ToString("o");
            }
            catch
            {
                return null;
            }
        }
    }
}
