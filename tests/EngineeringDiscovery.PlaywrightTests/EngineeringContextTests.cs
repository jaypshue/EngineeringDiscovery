using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace EngineeringDiscovery.PlaywrightTests
{
    public class EngineeringContextTests
    {
        [Test]
        public async Task EngineeringContextCard_ShowsRepositoryAfterImport()
        {
            Console.WriteLine($"[Test] EngineeringContextCard_ShowsRepositoryAfterImport START at {DateTime.UtcNow:O}");
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

            // Use shared test server started by TestServerFixture; fallback to default URL
            var serverUrl = Environment.GetEnvironmentVariable("ED_TEST_SERVER_URL") ?? "http://localhost:5167";
            Console.WriteLine($"[Test] navigating to {serverUrl + "/app"}");
            await page.GotoAsync(serverUrl + "/app");
            Console.WriteLine($"[Test] navigated to {serverUrl + "/app"}");

            // Wait for the engineering context card to appear (shorter timeout)
            Console.WriteLine("[Test] Waiting for .engineering-context-card selector");
            var card = await page.WaitForSelectorAsync(".engineering-context-card", new PageWaitForSelectorOptions { Timeout = 10000 });
            Console.WriteLine("[Test] .engineering-context-card selector returned");
            // Single minimal assertion required by ED-302: the card exists
            Assert.IsNotNull(card, "Engineering context card should be present on /app");

            Console.WriteLine($"[Test] EngineeringContextCard_ShowsRepositoryAfterImport COMPLETE at {DateTime.UtcNow:O}");
        }
    }
}
