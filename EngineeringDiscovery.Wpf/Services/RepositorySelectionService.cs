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
        private CancellationTokenSource? _cts;

        public RepositorySelectionService(WorkspaceState workspaceState)
        {
            _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
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
            SelectedPath = path;
            _ = DoServerDetectAsync();
            Notify();
            return Task.CompletedTask;
        }

        private async Task DoServerDetectAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            IsDetecting = true;
            ErrorMessage = null;
            Notify();

            try
            {
                if (string.IsNullOrWhiteSpace(SelectedPath) || !Directory.Exists(SelectedPath))
                {
                    DetectedType = RepoType.None;
                    ErrorMessage = "Folder does not exist.";
                    IsImportEnabled = false;
                    return;
                }

                DetectedName = Path.GetFileName(SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                var slnCount = Directory.EnumerateFiles(SelectedPath, "*.sln", SearchOption.TopDirectoryOnly).Count();
                var slnxCount = Directory.EnumerateFiles(SelectedPath, "*.slnx", SearchOption.TopDirectoryOnly).Count();
                var csprojCount = Directory.EnumerateFiles(SelectedPath, "*.csproj", SearchOption.AllDirectories).Count();
                var pomCount = Directory.EnumerateFiles(SelectedPath, "pom.xml", SearchOption.AllDirectories).Count();
                var gradleCount =
                    Directory.EnumerateFiles(SelectedPath, "build.gradle", SearchOption.AllDirectories).Count() +
                    Directory.EnumerateFiles(SelectedPath, "build.gradle.kts", SearchOption.AllDirectories).Count() +
                    Directory.EnumerateFiles(SelectedPath, "settings.gradle", SearchOption.AllDirectories).Count() +
                    Directory.EnumerateFiles(SelectedPath, "settings.gradle.kts", SearchOption.AllDirectories).Count();

                DetectedProjectCount = Math.Max(csprojCount, Math.Max(pomCount, gradleCount));
                if (slnCount > 0 || slnxCount > 0 || csprojCount > 0)
                {
                    DetectedType = RepoType.DotNet;
                }
                else if (pomCount > 0)
                {
                    DetectedType = RepoType.JavaMaven;
                }
                else if (gradleCount > 0)
                {
                    DetectedType = RepoType.JavaGradle;
                }
                else
                {
                    DetectedType = RepoType.None;
                    ErrorMessage = "No supported project files found (.csproj, pom.xml, build.gradle).";
                }

                IsImportEnabled = DetectedType != RepoType.None;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsDetecting = false;
                Notify();
            }
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

                var workspace = new global::EngineeringDiscovery.Core.Domain.Workspace.Workspace
                {
                    RepositoryPath = SelectedPath,
                    Investigation = investigation,
                    CurrentTask = null,
                    SelectedRole = EngineeringDiscovery.Core.Domain.Models.EngineeringRole.Developer
                };

                _workspaceState.ReplaceWorkspace(workspace);
                _workspaceState.SetInvestigation(investigation);
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

        private void Notify() => StateChanged?.Invoke();

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
