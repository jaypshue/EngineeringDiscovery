using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Workspace;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// File-backed implementation of IWorkspacePersistence.
    /// Responsible for resolving the storage location and performing atomic saves.
    /// </summary>
    public sealed class FileWorkspacePersistence : IWorkspacePersistence
    {
        private const string AppFolderName = "EngineeringDiscovery";
        private const string WorkspaceFileName = "workspace.json";
        private readonly string _workspaceFilePath;

        public FileWorkspacePersistence(string? folderPath = null)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var appFolder = Path.Combine(localAppData, AppFolderName);
                if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);
                _workspaceFilePath = Path.Combine(appFolder, WorkspaceFileName);
            }
            else
            {
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                _workspaceFilePath = Path.Combine(folderPath, WorkspaceFileName);
            }
        }

        public async Task<Workspace?> LoadAsync()
        {
            try
            {
                if (!File.Exists(_workspaceFilePath)) return null;
                var json = await File.ReadAllTextAsync(_workspaceFilePath).ConfigureAwait(false);
                var ws = JsonSerializer.Deserialize<Workspace>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return ws;
            }
            catch
            {
                // swallow and return null on failure; hosts may log via injected loggers in future
                return null;
            }
        }

        public async Task SaveAsync(Workspace? workspace)
        {
            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(_workspaceFilePath);
                if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(workspace, new JsonSerializerOptions { WriteIndented = true });

                // Atomic write: write to temp file then replace
                var tmp = _workspaceFilePath + ".tmp";
                await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
                // Backup existing file if present
                if (File.Exists(_workspaceFilePath))
                {
                    var bak = _workspaceFilePath + ".bak";
                    try { File.Copy(_workspaceFilePath, bak, overwrite: true); } catch { }
                }
                File.Move(tmp, _workspaceFilePath, overwrite: true);
            }
            catch
            {
                // swallow failures for now; persistence errors are non-fatal for PoC
            }
        }
    }
}
