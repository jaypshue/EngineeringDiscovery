using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services.Persistence;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// File-backed implementation of IWorkspacePersistence.
    /// Responsible for resolving the storage location and performing atomic saves.
    /// Uses InvestigationJsonConverter to ensure Investigation data survives round-trips.
    /// </summary>
    public sealed class FileWorkspacePersistence : IWorkspacePersistence
    {
        private const string AppFolderName = "EngineeringDiscovery";
        private const string WorkspaceFileName = "workspace.json";
        private readonly string _workspaceFilePath;
        private static readonly JsonSerializerOptions s_serializeOptions = CreateSerializerOptions(writeIndented: true);
        private static readonly JsonSerializerOptions s_deserializeOptions = CreateSerializerOptions(writeIndented: false);

        private static JsonSerializerOptions CreateSerializerOptions(bool writeIndented)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = writeIndented
            };
            options.Converters.Add(new InvestigationJsonConverter());
            return options;
        }

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
                var pathToLoad = _workspaceFilePath;
                if (!File.Exists(pathToLoad))
                {
                    // Fall back to backup if primary is missing (e.g., interrupted save)
                    var bakPath = _workspaceFilePath + ".bak";
                    if (File.Exists(bakPath))
                    {
                        pathToLoad = bakPath;
                    }
                    else
                    {
                        return null;
                    }
                }
                var json = await File.ReadAllTextAsync(pathToLoad).ConfigureAwait(false);
                var ws = JsonSerializer.Deserialize<Workspace>(json, s_deserializeOptions);
                return ws;
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveAsync(Workspace? workspace)
        {
            // Ensure directory exists. Failures intentionally propagate to WorkspaceState,
            // which converts them into an observable persistence result for the workflow.
            var dir = Path.GetDirectoryName(_workspaceFilePath);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(workspace, s_serializeOptions);

            // Atomic write: write to temp file then replace.
            var tmp = _workspaceFilePath + ".tmp";
            await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
            // Backup existing file if present. A backup failure must not hide failure of the
            // primary write, but it also must not prevent a valid primary save.
            if (File.Exists(_workspaceFilePath))
            {
                var bak = _workspaceFilePath + ".bak";
                try { File.Copy(_workspaceFilePath, bak, overwrite: true); } catch { }
            }
            File.Move(tmp, _workspaceFilePath, overwrite: true);
        }
    }
}
