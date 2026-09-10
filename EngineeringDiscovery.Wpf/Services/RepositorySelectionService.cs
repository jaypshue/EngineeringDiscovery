using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
// Use Win32 folder picker via WinForms reference; add alias to avoid ambiguous System.Windows namespace
using WinForms = System.Windows.Forms;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Wpf.Services
{
    public enum RepoType
    {
        None,
        DotNet,
        JavaMaven,
        JavaGradle
    }

    public class RepositorySelectionService : IDisposable
    {
        private readonly WorkspaceState _workspaceState;
        private readonly IWorkspacePersistence _persistence;
        private CancellationTokenSource? _cts;

        public RepositorySelectionService(WorkspaceState workspaceState, IWorkspacePersistence persistence)
        {
            _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public string? SelectedPath { get; private set; }
        public RepoType DetectedType { get; private set; } = RepoType.None;
        public string DetectedName { get; private set; } = string.Empty;
        public int DetectedProjectCount { get; private set; }
        public bool IsDetecting { get; private set; }
        public bool IsImportEnabled { get; private set; }
        public string? ErrorMessage { get; private set; }

        public event Action? StateChanged;

        public async Task PickFolderAsync()
        {
            // Use WinForms FolderBrowserDialog for simplicity and compatibility
            using var d = new WinForms.FolderBrowserDialog();
            d.Description = "Select repository folder";
            d.UseDescriptionForTitle = true;
            var res = d.ShowDialog();
            if (res == DialogResult.OK || res == DialogResult.Yes)
            {
                await SelectPathAsync(d.SelectedPath);
            }
        }

        public Task SelectPathAsync(string? path)
        {
            SelectedPath = global::EngineeringDiscovery.Core.Domain.Workspace.Workspace.NormalizeRepositoryPath(path);
            _ = DoServerDetectAsync();
            Notify();
            return Task.CompletedTask;
        }

        private async Task DoServerDetectAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var selectedPath = SelectedPath;
            IsDetecting = true;
            ErrorMessage = null;
            Notify();

            try
            {
                var result = await Task.Run(() => DetectRepository(selectedPath, ct), ct).ConfigureAwait(true);
                DetectedName = result.Name;
                DetectedProjectCount = result.ProjectCount;
                DetectedType = result.Type;
                ErrorMessage = result.ErrorMessage;
                IsImportEnabled = result.Type != RepoType.None;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                IsImportEnabled = false;
            }
            finally
            {
                IsDetecting = false;
                Notify();
            }
        }

        private static DetectionResult DetectRepository(string? path, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return new DetectionResult(string.Empty, 0, RepoType.None, "Folder does not exist.");
            }

            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var slnCount = Directory.EnumerateFiles(path, "*.sln", SearchOption.TopDirectoryOnly).Count();
            var slnxCount = Directory.EnumerateFiles(path, "*.slnx", SearchOption.TopDirectoryOnly).Count();
            var csprojCount = Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories).Count();
            var pomCount = Directory.EnumerateFiles(path, "pom.xml", SearchOption.AllDirectories).Count();
            var gradleCount =
                Directory.EnumerateFiles(path, "build.gradle", SearchOption.AllDirectories).Count() +
                Directory.EnumerateFiles(path, "build.gradle.kts", SearchOption.AllDirectories).Count() +
                Directory.EnumerateFiles(path, "settings.gradle", SearchOption.AllDirectories).Count() +
                Directory.EnumerateFiles(path, "settings.gradle.kts", SearchOption.AllDirectories).Count();

            ct.ThrowIfCancellationRequested();
            var projectCount = Math.Max(csprojCount, Math.Max(pomCount, gradleCount));
            var type = slnCount > 0 || slnxCount > 0 || csprojCount > 0
                ? RepoType.DotNet
                : pomCount > 0
                    ? RepoType.JavaMaven
                    : gradleCount > 0
                        ? RepoType.JavaGradle
                        : RepoType.None;
            var error = type == RepoType.None
                ? "No supported project files found (.csproj, pom.xml, build.gradle)."
                : null;

            return new DetectionResult(name, projectCount, type, error);
        }

        public async Task<bool> ImportAsync()
        {
            if (!IsImportEnabled || string.IsNullOrWhiteSpace(SelectedPath)) return false;

            IsDetecting = true;
            Notify();

            try
            {
                // Use the host-local InvestigationEngine implementation
                // Use the shared InvestigationEngine from the Web project. This project references the Web
                // assembly to reuse discovery logic.
                var engine = new EngineeringDiscovery.Web.Services.InvestigationEngine();
                var investigation = await Task.Run(() => engine.CreateInvestigation(SelectedPath, null));
                if (investigation is null)
                {
                    ErrorMessage = "Investigation creation failed.";
                    return false;
                }

                var now = DateTime.UtcNow;
                var ws = CloneWorkspace(_workspaceState.ActiveWorkspace);
                ws.RepositoryPath = SelectedPath;

                var existingProject = ws.FindProjectForRepositoryPath(SelectedPath);
                global::EngineeringDiscovery.Core.Domain.Workspace.Project project;
                global::EngineeringDiscovery.Core.Domain.Workspace.ImportedRepository imported;

                if (existingProject is not null)
                {
                    project = existingProject;
                    ws.ActiveProjectId = project.Id;
                    ws.ProjectState = project.State;

                    var importedIndex = ws.ImportedRepositories.FindIndex(repository =>
                        global::EngineeringDiscovery.Core.Domain.Workspace.Workspace.PathsEqual(repository.RepositoryPath, SelectedPath));
                    if (importedIndex >= 0)
                    {
                        var previous = ws.ImportedRepositories[importedIndex];
                        imported = new global::EngineeringDiscovery.Core.Domain.Workspace.ImportedRepository
                        {
                            RepositoryPath = SelectedPath,
                            ProjectId = project.Id,
                            Investigation = investigation,
                            CreatedUtc = previous.CreatedUtc,
                            LastBuiltUtc = previous.LastBuiltUtc,
                            RepositoryFingerprint = previous.RepositoryFingerprint
                        };
                        ws.ImportedRepositories[importedIndex] = imported;
                    }
                    else
                    {
                        imported = new global::EngineeringDiscovery.Core.Domain.Workspace.ImportedRepository
                        {
                            RepositoryPath = SelectedPath,
                            ProjectId = project.Id,
                            Investigation = investigation,
                            CreatedUtc = now
                        };
                        ws.ImportedRepositories.Add(imported);
                    }
                }
                else
                {
                    var projectName = Path.GetFileName(SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrWhiteSpace(projectName)) projectName = "Imported Project";

                    project = new global::EngineeringDiscovery.Core.Domain.Workspace.Project();
                    project.State.Identity = new global::EngineeringDiscovery.Core.Domain.ProjectState.ProjectIdentity
                    {
                        Name = projectName,
                        Description = $"Imported from {SelectedPath}",
                        EstablishedUtc = now,
                        LastUpdatedUtc = now
                    };
                    project.RepositoryPaths.Add(SelectedPath);
                    ws.Projects.Add(project);
                    ws.ActiveProjectId = project.Id;
                    ws.ProjectState = project.State;

                    imported = new global::EngineeringDiscovery.Core.Domain.Workspace.ImportedRepository
                    {
                        RepositoryPath = SelectedPath,
                        ProjectId = project.Id,
                        Investigation = investigation,
                        CreatedUtc = now
                    };
                    ws.ImportedRepositories.Add(imported);
                }

                // Keep legacy workspace-level values as compatibility projections while
                // the linked imported folder and Project.State remain authoritative.
                ws.Investigation = investigation;

                var builtUtc = DateTime.UtcNow;
                var fingerprint = _workspaceState.ComputeRepositoryFingerprint(SelectedPath);
                ws.SetFreshness(builtUtc, fingerprint);
                imported.LastBuiltUtc = builtUtc;
                imported.RepositoryFingerprint = fingerprint;

                // Persist the candidate before replacing the authoritative in-memory state.
                // A failed switch therefore leaves the currently active project untouched.
                await _persistence.SaveAsync(ws).ConfigureAwait(false);
                _workspaceState.ReplaceWorkspace(ws);
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return false;
            }
            finally
            {
                IsDetecting = false;
                Notify();
            }
        }

        private sealed record DetectionResult(string Name, int ProjectCount, RepoType Type, string? ErrorMessage);

        private static global::EngineeringDiscovery.Core.Domain.Workspace.Workspace CloneWorkspace(
            global::EngineeringDiscovery.Core.Domain.Workspace.Workspace? source)
        {
            if (source is null) return new global::EngineeringDiscovery.Core.Domain.Workspace.Workspace();

            return new global::EngineeringDiscovery.Core.Domain.Workspace.Workspace
            {
                Id = source.Id,
                SchemaVersion = source.SchemaVersion,
                RepositoryPath = source.RepositoryPath,
                Investigation = source.Investigation,
                CurrentTask = source.CurrentTask,
                SelectedRole = source.SelectedRole,
                CreatedUtc = source.CreatedUtc,
                LastModifiedUtc = source.LastModifiedUtc,
                ImportedRepositories = new System.Collections.Generic.List<global::EngineeringDiscovery.Core.Domain.Workspace.ImportedRepository>(source.ImportedRepositories ?? new()),
                CurrentActivity = source.CurrentActivity,
                Iterations = new System.Collections.Generic.List<global::EngineeringDiscovery.Core.Domain.Iteration.EngineeringIteration>(source.Iterations ?? new()),
                ProjectState = source.ProjectState,
                Projects = new System.Collections.Generic.List<global::EngineeringDiscovery.Core.Domain.Workspace.Project>(source.Projects ?? new()),
                ActiveProjectId = source.ActiveProjectId,
                LastBuiltUtc = source.LastBuiltUtc,
                RepositoryFingerprint = source.RepositoryFingerprint
            };
        }

        private void Notify() => StateChanged?.Invoke();

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
