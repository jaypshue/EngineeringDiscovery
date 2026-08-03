using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.TestInfrastructure
{
    public abstract class TestBase
    {
        protected Process? AppProcess;
        protected IPlaywright? Playwright;
        protected IBrowser? Browser;
        protected IPage? Page;

        // Capture child process output for diagnostics when it exits early
        private StringBuilder _stdOut = new();
        private StringBuilder _stdErr = new();

        private string SolutionRoot => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
        private string WorkspaceFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EngineeringDiscovery", "workspace.json");

        [SetUp]
        public async Task SetUp()
        {
            // Ensure no persisted workspace exists to isolate tests
            try { if (File.Exists(WorkspaceFilePath)) File.Delete(WorkspaceFilePath); } catch { }

            // Start the Blazor app for each test to ensure a clean process and state
            var projectPath = Path.Combine(SolutionRoot, "EngineeringDiscovery.Web", "EngineeringDiscovery.Web.csproj");
            var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" --urls http://localhost:5005")
            {
                WorkingDirectory = SolutionRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            AppProcess = Process.Start(startInfo);
            if (AppProcess == null)
            {
                throw new Exception("Failed to start dotnet run process for the Blazor app.");
            }
            // Capture stdout/stderr so buffers don't block and to provide diagnostics if the process exits early
            try
            {
                AppProcess.OutputDataReceived += (s, e) => { if (e?.Data != null) _stdOut.AppendLine(e.Data); };
                AppProcess.ErrorDataReceived += (s, e) => { if (e?.Data != null) _stdErr.AppendLine(e.Data); };
                try { AppProcess.BeginOutputReadLine(); AppProcess.BeginErrorReadLine(); } catch { }
            }
            catch { }
            // Wait for the app to start accepting HTTP connections
            var http = new System.Net.Http.HttpClient();
            var sw = Stopwatch.StartNew();
            var started = false;
            while (!started && sw.Elapsed < TimeSpan.FromSeconds(180))
            {
                // If the child process has already exited, fail fast with collected stdout/stderr
                try
                {
                    if (AppProcess != null && AppProcess.HasExited)
                    {
                        var code = AppProcess.ExitCode;
                        var outText = _stdOut.ToString();
                        var errText = _stdErr.ToString();
                        // Ensure process disposed
                        try { AppProcess.Dispose(); } catch { }
                        throw new Exception($"Blazor app process exited early (exitCode={code}).\nStdout:\n{outText}\nStderr:\n{errText}");
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    // If accessing HasExited throws, include that info and fail
                    var outText = _stdOut.ToString();
                    var errText = _stdErr.ToString();
                    throw new Exception($"Blazor app process state check failed: {ex.Message}.\nStdout:\n{outText}\nStderr:\n{errText}");
                }
                try
                {
                    var resp = await http.GetAsync("http://localhost:5005/");
                    if (resp.IsSuccessStatusCode)
                    {
                        started = true;
                        break;
                    }
                }
                catch
                {
                    // not listening yet
                }
                await Task.Delay(500);
            }
            if (!started)
            {
                // Ensure process is killed on failure to start (kill entire tree)
                try { if (AppProcess != null && !AppProcess.HasExited) AppProcess.Kill(entireProcessTree: true); } catch { try { if (AppProcess != null && !AppProcess.HasExited) AppProcess.Kill(); } catch { } }
                throw new Exception("Blazor app did not start within the expected time at http://localhost:5005/");
            }

            // Initialize Playwright and browser
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            // Ensure browsers installed is a no-op if already installed; this may be required first-run only.
            try { Microsoft.Playwright.Program.Main(new[] { "install" }); } catch { }
            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            Page = await Browser.NewPageAsync();

            // Navigate to app root
            await Page.GotoAsync("http://localhost:5005", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
        }

        [TearDown]
        public async Task TearDown()
        {
            try { if (Page != null) await Page.CloseAsync(); } catch { }
            try { if (Browser != null) await Browser.CloseAsync(); } catch { }
            try { Playwright?.Dispose(); } catch { }
            // Kill the dotnet run process and its child process tree, then wait briefly for exit
            try
            {
                if (AppProcess != null && !AppProcess.HasExited)
                {
                    try { AppProcess.Kill(entireProcessTree: true); } catch { try { AppProcess.Kill(); } catch { } }
                    AppProcess.WaitForExit(5000);
                }
            }
            catch { }
            try { AppProcess?.Dispose(); } catch { }
            // Cleanup workspace file after test
            try { var path = WorkspaceFilePath; if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
