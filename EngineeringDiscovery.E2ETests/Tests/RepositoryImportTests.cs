using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class RepositoryImportTests : TestInfrastructure.TestBase
    {
        // Per-test process and Playwright provided by TestBase

        [Test]
        public async Task Can_Import_Repository()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;
            await page.GotoAsync("http://localhost:5005", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // The repository input is on the dashboard (root). Ensure it's present before interacting.
            await page.WaitForSelectorAsync("input.repo-input", new PageWaitForSelectorOptions { Timeout = 15000 });

            // Use the solution root as the repository path so the discovery engine can read files.
            var solutionRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            await page.FillAsync("input.repo-input", solutionRoot);
            // Ensure Blazor processes the change (bind on change) by blurring the input
            await page.PressAsync("input.repo-input", "Tab");
            await page.ClickAsync("text=Import Repository");

            // Wait for an element that indicates import completed or discovery started
            await page.WaitForSelectorAsync("text=Engineering Model", new PageWaitForSelectorOptions { Timeout = 30000 });

            var content = await page.ContentAsync();
            Assert.IsTrue(content.Contains("Engineering Model") || content.Contains("Repository"), "Expected discovery or repository info to appear after import.");
        }
    }
}
