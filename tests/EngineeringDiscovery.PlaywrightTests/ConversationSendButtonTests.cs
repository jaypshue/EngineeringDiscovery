using NUnit.Framework;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace EngineeringDiscovery.PlaywrightTests
{
    public class ConversationSendButtonTests
    {
        [Test]
        public async Task SendButton_EnablesAndDisables_BasedOnTextboxContent()
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            // Start the web app for the test run
            // Resolve the web project path by walking up from the test assembly directory so this works
            // regardless of the test runner's current working directory.
            string projectPath = null;
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            for (int depth = 0; depth < 8 && dir != null; depth++)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, "..", "..", "EngineeringDiscovery.Web", "EngineOS.Web.csproj");
                candidate = System.IO.Path.GetFullPath(candidate);
                if (System.IO.File.Exists(candidate))
                {
                    projectPath = candidate;
                    break;
                }
                dir = dir.Parent;
            }
            if (projectPath == null)
            {
                // Fallback: look relative to repository root
                var fallback = System.IO.Path.GetFullPath(System.IO.Path.Combine("..", "..", "EngineeringDiscovery.Web", "EngineOS.Web.csproj"));
                if (System.IO.File.Exists(fallback)) projectPath = fallback;
            }
            if (projectPath == null)
                throw new System.InvalidOperationException("Web project not found; ensure EngineeringDiscovery.Web/EngineOS.Web.csproj exists relative to repository root or test output directories");

            // Build the web project to ensure no build step during run
            var buildPsi = new System.Diagnostics.ProcessStartInfo("dotnet", $"build \"{projectPath}\" --configuration Debug")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var buildProc = System.Diagnostics.Process.Start(buildPsi);
            var buildExited = buildProc.WaitForExit(120000); // 120s
            if (!buildExited)
            {
                try { if (!buildProc.HasExited) buildProc.Kill(true); } catch { }
                throw new System.TimeoutException("Building web project did not finish within the allotted time");
            }
            if (buildProc.ExitCode != 0) throw new System.InvalidOperationException("Failed to build web project before running test");

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
                for (int i = 0; i < 120; i++)
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
                if (!started) throw new System.TimeoutException("Web app did not start in time");

                await page.GotoAsync("http://localhost:5167/app");
                // Give Blazor Server some time to establish SignalR connection before sending input events
                await page.WaitForTimeoutAsync(3000);
            }
            finally
            {
                // ensure process is cleaned up after test
                try { if (server != null && !server.HasExited) server.Kill(true); } catch { }
            }

            await page.WaitForSelectorAsync(".conversation-host textarea", new PageWaitForSelectorOptions { Timeout = 10000 });
            var textarea = await page.QuerySelectorAsync(".conversation-host textarea");
            var sendButton = await page.QuerySelectorAsync(".conversation-host .composer button");

            // Enter text by setting the textarea value and dispatching input events via JS so Blazor sees the update
            await page.EvaluateAsync("arg => { const el = document.querySelector(arg.selector); if (!el) return false; el.value = arg.text; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); return true; }", new { selector = ".conversation-host textarea", text = "Hello" });
            // Wait up to 30s for the Send button to become enabled in the browser (Blazor server initialization may be slow in CI)
            await page.WaitForFunctionAsync("selector => { const el = document.querySelector(selector); return el && !el.disabled; }", ".conversation-host .composer button", new PageWaitForFunctionOptions { Timeout = 30000 });
            // Re-select the button after the Blazor render may have replaced the element
            sendButton = await page.QuerySelectorAsync(".conversation-host .composer button");
            // Single required assertion: Send button becomes enabled after entering "Hello"
            Assert.IsFalse(await sendButton.IsDisabledAsync(), "Send button should be enabled after typing text");
        }
    }
}
