using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.CurrentTask;
using System.Text.Json;
using EngineeringDiscovery.Core.Domain.Workspace;
using Microsoft.Extensions.Logging;

namespace EngineeringDiscovery.Core.Services
{
    // Note: EngineeringInsight currently lives in Core as a temporary measure during migration.
    // In ED-205 final state, presentation DTOs will be moved to presentation projects.
    public sealed record EngineeringInsight(string Subject, string Observation, string Category);
    // Core-owned WorkspaceState - single source of truth for application state
    public sealed class WorkspaceState
    {
        private const string AppFolderName = "EngineeringDiscovery";
        private const string WorkspaceFileName = "workspace.json";
        private readonly string _workspaceFilePath;
        private readonly ILogger<WorkspaceState>? _logger;

        public WorkspaceState(ILogger<WorkspaceState>? logger = null)
        {
            _logger = logger;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(localAppData, AppFolderName);
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);

            _workspaceFilePath = Path.Combine(appFolder, WorkspaceFileName);

            ActiveWorkspace = LoadWorkspace();
        }

        public Workspace? ActiveWorkspace { get; private set; }

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

        // Determine model freshness based on last built timestamp and repository fingerprint
        public EngineeringModelFreshness GetFreshnessStatus()
        {
            try
            {
                if (ActiveWorkspace is null) return EngineeringModelFreshness.Unknown;
                if (ActiveWorkspace.Investigation is null) return EngineeringModelFreshness.Unknown;
                if (!string.IsNullOrWhiteSpace(ActiveWorkspace.RepositoryPath))
                {
                    // If LastBuiltUtc is not set, require build
                    if (ActiveWorkspace.LastBuiltUtc == null) return EngineeringModelFreshness.RefreshRequired;

                    // Very simple heuristic: if repository fingerprint differs, recommend refresh
                    var fingerprint = ComputeRepositoryFingerprint(ActiveWorkspace.RepositoryPath ?? string.Empty);
                    if (fingerprint is null) return EngineeringModelFreshness.Unknown;
                    if (!string.Equals(fingerprint, ActiveWorkspace.RepositoryFingerprint, StringComparison.Ordinal)) return EngineeringModelFreshness.RefreshRecommended;

                    return EngineeringModelFreshness.Current;
                }

                return EngineeringModelFreshness.Unknown;
            }
            catch
            {
                return EngineeringModelFreshness.Unknown;
            }
        }

        private Workspace? LoadWorkspace()
        {
            try
            {
                if (!File.Exists(_workspaceFilePath)) return null;
                var json = File.ReadAllText(_workspaceFilePath);
                var ws = JsonSerializer.Deserialize<Workspace>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return ws;
            }
            catch
            {
                // Swallow errors in load for PoC. Logging can be added via adapters if needed.
                return null;
            }
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(ActiveWorkspace, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_workspaceFilePath, json);
            }
            catch
            {
                // Ignore save failures for PoC; host may provide logging adapters.
            }
        }

        // Register persistence hooks for legacy UI state holders (CurrentTaskState etc.) using reflection to avoid
        // introducing a hard dependency on UI projects. This method wires the UI state's OnChange to persist
        // the corresponding Core ActiveWorkspace state.
        public void RegisterPersistenceHooks(object? currentTaskState, object? investigationState)
        {
            try
            {
                if (currentTaskState is not null)
                {
                    var evt = currentTaskState.GetType().GetEvent("OnChange");
                    if (evt != null)
                    {
                        Action handler = () =>
                        {
                            try
                            {
                                var activeTaskProp = currentTaskState.GetType().GetProperty("ActiveTask");
                                var activeTask = activeTaskProp?.GetValue(currentTaskState);
                                if (ActiveWorkspace is null) ActiveWorkspace = new Workspace();
                                var wsCurrentTaskProp = typeof(Workspace).GetProperty("CurrentTask");
                                wsCurrentTaskProp?.SetValue(ActiveWorkspace, activeTask);
                                Save();
                                NotifyStateChanged();
                            }
                            catch { }
                        };

                        evt.AddEventHandler(currentTaskState, handler);
                    }
                }
            }
            catch { }
        }

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
            Save();
            NotifyStateChanged();
        }

        public void SetInvestigation(Domain.Investigation.Investigation? investigation)
        {
            if (ActiveWorkspace is null) ActiveWorkspace = new Workspace();
            ActiveWorkspace.Investigation = investigation;
            Save();
            NotifyStateChanged();
        }

        // ----- Backwards-compatibility helpers for UI layers that previously lived in Web project -----
        public void UpdateBrief(Action<EngineeringDiscovery.Core.Domain.CurrentTask.EngineeringBrief> update)
        {
            try
            {
                var brief = ActiveWorkspace?.CurrentTask?.Brief;
                if (brief is null) return;
                update(brief);
                Save();
                NotifyStateChanged();
            }
            catch { }
        }

        public void BeginTask(string title, string description, string goal)
        {
            try
            {
                if (ActiveWorkspace is null) ActiveWorkspace = new Workspace();
                ActiveWorkspace.CurrentTask = new CurrentTask(title, description, goal);
                Save();
                NotifyStateChanged();
            }
            catch { }
        }

        public void CompleteTask()
        {
            try
            {
                if (ActiveWorkspace?.CurrentTask is null) return;
                ActiveWorkspace.CurrentTask.Complete();
                ActiveWorkspace.CurrentTask = null;
                Save();
                NotifyStateChanged();
            }
            catch { }
        }

        public void AddContext(string kind, string id)
        {
            try
            {
                var ctx = ActiveWorkspace?.CurrentTask?.Brief?.Context;
                if (ctx is null) return;
                switch (kind)
                {
                    case "Project": ctx.AddProject(id); break;
                    case "Namespace": ctx.AddNamespace(id); break;
                    case "Type": ctx.AddType(id); break;
                    default: break;
                }
                Save();
                NotifyStateChanged();
            }
            catch { }
        }

        public IEnumerable<string> GetTypeRecommendations()
        {
            // Placeholder: UI previously displayed recommendations produced by web services.
            return Enumerable.Empty<string>();
        }

        public IEnumerable<EngineeringInsight> GetInsights()
        {
            return Enumerable.Empty<EngineeringInsight>();
        }

        public string AskAdvisor(string question)
        {
            // Stubbed advisor - hosts can implement richer behavior in UI services.
            return string.Empty;
        }

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
