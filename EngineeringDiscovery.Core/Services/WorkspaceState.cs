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
                if (ActiveWorkspace.Investigation is null) return EngineeringModelFreshness.Unknown;
                if (string.IsNullOrWhiteSpace(ActiveWorkspace.RepositoryPath)) return EngineeringModelFreshness.Unknown;

                // Host-provided service evaluates freshness according to configured policy
                var task = _fingerprintService.EvaluateFreshnessAsync(ActiveWorkspace.RepositoryPath, ActiveWorkspace.LastBuiltUtc, ActiveWorkspace.RepositoryFingerprint);
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

        // Persistence delegated to IWorkspacePersistence implementation supplied by host
        public void Save()
        {
            try
            {
                _persistence.SaveAsync(ActiveWorkspace).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore save failures; host logging can report if desired.
            }
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
            ActiveWorkspace = workspace;
            // Do not perform persistence here; persistence is the responsibility of workflow services.
            NotifyStateChanged();
        }

        public void SetInvestigation(Domain.Investigation.Investigation? investigation)
        {
            if (ActiveWorkspace is null) ActiveWorkspace = new Workspace();
            ActiveWorkspace.Investigation = investigation;
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
