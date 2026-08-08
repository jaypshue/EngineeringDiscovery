using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace EngineeringDiscovery.PlaywrightTests
{
    // Assembly-level fixture that builds and starts the web app once for the entire test run.
    // This drastically reduces test time by avoiding per-test build/start cycles.
    [SetUpFixture]
    public class TestServerFixture
    {
        private Process? _server;
        private const string ServerUrl = "http://localhost:5167";

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            try
            {
                Console.WriteLine($"[TestServerFixture] Starting OneTimeSetUp at {DateTime.UtcNow:O}");
                // Locate the web project (reuse the same heuristic as the tests)
                string? projectPath = null;
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                for (int depth = 0; depth < 8 && dir != null; depth++)
                {
                    var candidate = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "EngineeringDiscovery.Web", "EngineOS.Web.csproj"));
                    if (File.Exists(candidate))
                    {
                        projectPath = candidate;
                        break;
                    }
                    dir = dir.Parent;
                }
                if (projectPath == null)
                {
                    var fallback = Path.GetFullPath(Path.Combine("..", "..", "EngineeringDiscovery.Web", "EngineOS.Web.csproj"));
                    if (File.Exists(fallback)) projectPath = fallback;
                }

                if (projectPath == null)
                {
                    Assert.Fail("Web project not found; ensure EngineeringDiscovery.Web/EngineOS.Web.csproj exists relative to repository root");
                    return;
                }

                // Build once (short timeout)
                Console.WriteLine("[TestServerFixture] Building web project...");
                var buildPsi = new ProcessStartInfo("dotnet", $"build \"{projectPath}\" --configuration Debug")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var buildProc = Process.Start(buildPsi);
                if (buildProc == null) Assert.Fail("Failed to start dotnet build process");
                var buildExited = buildProc.WaitForExit(120000); // 120s
                if (!buildExited)
                {
                    try { if (!buildProc.HasExited) buildProc.Kill(true); } catch { }
                    Assert.Fail("Building web project did not finish within the allotted time");
                }
                if (buildProc.ExitCode != 0)
                {
                    var outText = await buildProc.StandardOutput.ReadToEndAsync();
                    var errText = await buildProc.StandardError.ReadToEndAsync();
                    Assert.Fail($"Failed to build web project before running tests. stdout:\n{outText}\nstderr:\n{errText}");
                }

                // Start the server once for the entire assembly
                var psi = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" --no-build --urls {ServerUrl}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                Console.WriteLine($"[TestServerFixture] Starting server with: {psi.FileName} {psi.Arguments}");
                _server = Process.Start(psi);

                if (_server == null)
                {
                    Assert.Fail("Failed to start web app process");
                    return;
                }

                // Wait for the app to accept connections (shorter timeout), but supervise the process.
                using var http = new HttpClient();
                var started = false;
                for (int i = 0; i < 40; i++) // ~20s
                {
                    // If the child process has exited, read its stdout/stderr and fail immediately.
                    try
                    {
                        if (_server.HasExited)
                        {
                            var outText = await _server.StandardOutput.ReadToEndAsync();
                            var errText = await _server.StandardError.ReadToEndAsync();
                            var exitCode = _server.ExitCode;
                            Console.WriteLine($"[TestServerFixture] Server process exited early (code={exitCode}). stdout:\n{outText}\nstderr:\n{errText}");
                            Assert.Fail($"Web app process exited with code {exitCode}. stdout:\n{outText}\nstderr:\n{errText}");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        // If reading streams fails, log and fail
                        Console.WriteLine($"[TestServerFixture] Failed while supervising server process: {ex.Message}");
                        try { if (_server != null && !_server.HasExited) _server.Kill(true); } catch { }
                        Assert.Fail("Web app process supervision failed: " + ex.Message);
                        return;
                    }

                    try
                    {
                        var r = await http.GetAsync(ServerUrl + "/app");
                        if (r.IsSuccessStatusCode)
                        {
                            started = true;
                            Console.WriteLine($"[TestServerFixture] Server accepting requests at {ServerUrl}/app (iteration {i})");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[TestServerFixture] Server not ready yet: {ex.Message}");
                    }
                    await Task.Delay(500);
                }
                if (!started)
                {
                    try
                    {
                        if (_server != null)
                        {
                            // Attempt to capture any remaining output
                            string outText = string.Empty;
                            string errText = string.Empty;
                            try { outText = await _server.StandardOutput.ReadToEndAsync(); } catch { }
                            try { errText = await _server.StandardError.ReadToEndAsync(); } catch { }
                            try { if (!_server.HasExited) _server.Kill(true); } catch { }
                            Console.WriteLine($"[TestServerFixture] Server did not start in time. stdout:\n{outText}\nstderr:\n{errText}");
                        }
                    }
                    catch { }
                    Assert.Fail("Web app did not start in time");
                }

                Console.WriteLine($"[TestServerFixture] Server started and reachable at {ServerUrl}");

                // Expose the shared server URL to the test processes
                Environment.SetEnvironmentVariable("ED_TEST_SERVER_URL", ServerUrl, EnvironmentVariableTarget.Process);
            }
            catch (Exception ex)
            {
                Assert.Fail("Test server fixture failed to start: " + ex.Message);
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            try
            {
                Environment.SetEnvironmentVariable("ED_TEST_SERVER_URL", null, EnvironmentVariableTarget.Process);
                if (_server != null && !_server.HasExited)
                {
                    _server.Kill(true);
                    _server.Dispose();
                    _server = null;
                }
            }
            catch { }
        }
    }
}
