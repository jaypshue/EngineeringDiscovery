using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace EngineeringDiscovery.Web.Services
{
    public class RepositorySelectionService : IRepositorySelectionService, IDisposable
    {
        private readonly IJSRuntime _js;
        private Timer? _debounceTimer;
        private CancellationTokenSource? _cts;

        public RepositorySelectionService(IJSRuntime js)
        {
            _js = js ?? throw new ArgumentNullException(nameof(js));
        }

        public string? SelectedPath { get; private set; }
        public RepoType DetectedType { get; private set; } = RepoType.None;
        public string DetectedName { get; private set; } = string.Empty;
        public int DetectedProjectCount { get; private set; }
        public bool ClientSelectionDetected { get; private set; }
        public bool IsDetecting { get; private set; }
        public bool IsImportEnabled { get; private set; }
        public string? ErrorMessage { get; private set; }

        public event Action? StateChanged;

        public Task SelectPathAsync(string? path)
        {
            // Cancel pending client detection and schedule server-side validation with debounce
            ClientSelectionDetected = false;
            SelectedPath = path;
            StartDebouncedDetect();
            Notify();
            return Task.CompletedTask;
        }

        public async Task SelectClientSummaryAsync(object summaryObj)
        {
            // Parse the JS-provided summary similarly to the component's previous logic
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(summaryObj);
                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                string name = string.Empty;
                string typeStr = "None";
                int projects = 0;

                if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var arr = root.EnumerateArray().ToArray();
                    var sampleFiles = arr.Select(e =>
                    {
                        if (e.ValueKind != System.Text.Json.JsonValueKind.Object) return string.Empty;
                        if (e.TryGetProperty("webkitRelativePath", out var wr) && wr.ValueKind == System.Text.Json.JsonValueKind.String) return wr.GetString() ?? string.Empty;
                        if (e.TryGetProperty("path", out var pth) && pth.ValueKind == System.Text.Json.JsonValueKind.String) return pth.GetString() ?? string.Empty;
                        if (e.TryGetProperty("name", out var nm) && nm.ValueKind == System.Text.Json.JsonValueKind.String) return nm.GetString() ?? string.Empty;
                        return string.Empty;
                    }).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

                    if (sampleFiles.Length > 0)
                    {
                        var first = sampleFiles[0];
                        if (Path.IsPathRooted(first))
                        {
                            name = Path.GetDirectoryName(first) ?? first;
                        }
                        else
                        {
                            var parts = first.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            name = parts.Length > 0 ? parts[0] : first;
                        }
                    }

                    typeStr = "None";
                    projects = 0;
                    foreach (var f in sampleFiles)
                    {
                        var lf = f.ToLowerInvariant();
                        if (lf.EndsWith(".sln") || lf.EndsWith(".slnx")) typeStr = "DotNet";
                        if (lf.EndsWith(".csproj")) { typeStr = "DotNet"; projects++; }
                        if (lf.EndsWith("pom.xml")) { if (typeStr == "None") typeStr = "JavaMaven"; projects++; }
                        if (lf.EndsWith("build.gradle") || lf.EndsWith("settings.gradle") || lf.EndsWith("build.gradle.kts") || lf.EndsWith("settings.gradle.kts")) { if (typeStr == "None") typeStr = "JavaGradle"; projects++; }
                    }
                }
                else
                {
                    name = root.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    typeStr = root.TryGetProperty("detectedType", out var t) ? t.GetString() ?? "None" : "None";
                    projects = root.TryGetProperty("detectedProjectCount", out var p) ? p.GetInt32() : 0;
                }

                SelectedPath = name;
                DetectedName = name;
                DetectedProjectCount = projects;
                DetectedType = typeStr switch
                {
                    "DotNet" => RepoType.DotNet,
                    "JavaMaven" => RepoType.JavaMaven,
                    "JavaGradle" => RepoType.JavaGradle,
                    _ => RepoType.None
                };

                ClientSelectionDetected = DetectedType != RepoType.None;
                // Client selection enables import immediately (server validation still occurs on Import)
                UpdateImportEnabled();
                Notify();
            }
            catch { }

            await Task.CompletedTask;
        }

        private void StartDebouncedDetect()
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(async _ => await DoServerDetectAsync(), null, 350, Timeout.Infinite);
        }

        private async Task DoServerDetectAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            IsDetecting = true;
            Notify();

            try
            {
                // Server-side validation: check if folder exists and detect project files
                if (string.IsNullOrWhiteSpace(SelectedPath) || !Directory.Exists(SelectedPath))
                {
                    DetectedType = RepoType.None;
                    ErrorMessage = "Folder does not exist.";
                }
                else
                {
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
                    }

                    ErrorMessage = null;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsDetecting = false;
                UpdateImportEnabled();
                Notify();
            }
        }

        private void UpdateImportEnabled()
        {
            IsImportEnabled = DetectedType != RepoType.None && (!string.IsNullOrWhiteSpace(SelectedPath) && Directory.Exists(SelectedPath) || ClientSelectionDetected);
        }

        private void Notify() => StateChanged?.Invoke();

        public void Dispose()
        {
            _debounceTimer?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
