using System;
using System.IO;
using System.Text.Json;
using EngineeringDiscovery.Core.Domain.Workspace;
using Microsoft.Extensions.Logging;

namespace EngineeringDiscovery.Web.Services
{
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

        public void ImportRepository(string repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath)) throw new ArgumentException("repositoryPath is required", nameof(repositoryPath));

            var workspace = new Workspace
            {
                RepositoryPath = repositoryPath.Trim(),
                Investigation = null,
                CurrentTask = null
            };

            ActiveWorkspace = workspace;
            Save();
            NotifyStateChanged();

            // Discovery pipeline should be invoked here to populate Investigation.
            // ImportRepository currently creates the persisted workspace record; callers should populate Investigation
            // and then call ReplaceWorkspace(workspace) to persist a fully initialized workspace.
        }

        public void Save()
        {
            try
            {
                if (ActiveWorkspace is null) return;

                ActiveWorkspace.Touch();

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(ActiveWorkspace, options);
                File.WriteAllText(_workspaceFilePath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save workspace to {Path}", _workspaceFilePath);
            }
        }

        private Workspace? LoadWorkspace()
        {
            try
            {
                if (!File.Exists(_workspaceFilePath)) return null;

                var json = File.ReadAllText(_workspaceFilePath);
                var workspace = JsonSerializer.Deserialize<Workspace>(json);
                return workspace;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load workspace from {Path}", _workspaceFilePath);
                return null;
            }
        }

        public void ReplaceWorkspace(Workspace newWorkspace)
        {
            ActiveWorkspace = newWorkspace ?? throw new ArgumentNullException(nameof(newWorkspace));
            Save();
            NotifyStateChanged();
        }

        // Subscribe to external state changes so the Workspace can be persisted when underlying components mutate.
        public void RegisterPersistenceHooks(EngineeringDiscovery.Web.Services.CurrentTaskState currentTaskState, EngineeringDiscovery.Web.Services.InvestigationState investigationState)
        {
            // When compatibility state changes, copy the latest values into the active Workspace and persist.
            if (currentTaskState is not null)
            {
                currentTaskState.OnChange += () =>
                {
                    try
                    {
                        if (ActiveWorkspace is null) return;
                        ActiveWorkspace.CurrentTask = currentTaskState.ActiveTask;
                        Save();
                    }
                    catch { }
                };
            }

            if (investigationState is not null)
            {
                investigationState.OnChange += () =>
                {
                    try
                    {
                        if (ActiveWorkspace is null) return;
                        ActiveWorkspace.Investigation = investigationState.Investigation;
                        Save();
                    }
                    catch { }
                };
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
