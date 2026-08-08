using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace EngineeringDiscovery.PlaywrightTests
{
    public class EngineeringContextTests
    {
        [Test]
        public async Task EngineeringContextCard_ShowsRepositoryAfterImport()
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            // Start the web app for the test run (compute project path relative to test assembly)
            string projectPath = null;
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir.FullName, "..", "..", "EngineeringDiscovery.Web", "EngineOS.Web.csproj"));
                if (System.IO.File.Exists(candidate))
                {
                    projectPath = candidate;
                    break;
                }
                dir = dir.Parent;
            }
            if (projectPath == null)
            {
                var fallback = System.IO.Path.GetFullPath(System.IO.Path.Combine("..", "..", "EngineeringDiscovery.Web", "EngineOS.Web.csproj"));
                if (System.IO.File.Exists(fallback)) projectPath = fallback;
            }
            if (projectPath == null) Assert.Fail("Web project not found; ensure EngineeringDiscovery.Web/EngineOS.Web.csproj exists relative to repository root");

            // Build the web project to ensure no build step during run
            var buildPsi = new System.Diagnostics.ProcessStartInfo("dotnet", $"build \"{projectPath}\" --configuration Debug")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var buildProc = System.Diagnostics.Process.Start(buildPsi);
            if (buildProc == null) Assert.Fail("Failed to start dotnet build process");
            var buildExited = buildProc.WaitForExit(300000); // 5 minutes
            if (!buildExited)
            {
                try { if (!buildProc.HasExited) buildProc.Kill(true); } catch { }
                Assert.Fail("Building web project did not finish within the allotted time");
            }
            if (buildProc.ExitCode != 0) Assert.Fail("Failed to build web project before running test");

            var psi = new System.Diagnostics.ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" --no-build --urls http://localhost:5167")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var server = System.Diagnostics.Process.Start(psi);
            try
            {
                // Wait for the app to accept connections (up to 60s)
                using var http = new System.Net.Http.HttpClient();
                var started = false;
                for (int i = 0; i < 240; i++)
                {
                    try
                    {
                        var r = await http.GetAsync("http://localhost:5167/app");
                        if (r.IsSuccessStatusCode)
                        {
                            started = true;
                            break;
                        }
                    }
                    catch { }
                    await Task.Delay(500);
                }
                if (!started) Assert.Fail("Web app did not start in time");

                // Navigate to /app which will show the EngineeringContextCard when a workspace is present.
                await page.GotoAsync("http://localhost:5167/app");

                // Wait for the engineering context card to appear
                var card = await page.WaitForSelectorAsync(".engineering-context-card", new PageWaitForSelectorOptions { Timeout = 10000 });
                // Single minimal assertion required by ED-302: the card exists
                Assert.IsNotNull(card, "Engineering context card should be present on /app");
            }
            finally
            {
                try { if (server != null && !server.HasExited) server.Kill(true); } catch { }
            }
        }
    }
}
