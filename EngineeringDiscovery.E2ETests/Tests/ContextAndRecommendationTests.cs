using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class ContextAndRecommendationTests : TestInfrastructure.TestBase
    {
        // Per-test process and Playwright provided by TestBase

        [Test]
        public async Task Can_Add_And_Remove_Context()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;
            await page.GotoAsync("http://localhost:5005/workspace", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Ensure a current task exists; create if necessary
            if (!(await page.ContentAsync()).Contains("Complete Task"))
            {
                await page.FillAsync("input[placeholder='Short task title']", "E2E Task");
                await page.FillAsync("textarea[placeholder='What work is being performed?']", "Describe");
                await page.FillAsync("textarea[placeholder='What outcome should this work produce?']", "Goal");
                await page.ClickAsync("text=Begin Current Task");
                await page.WaitForSelectorAsync("text=Complete Task", new PageWaitForSelectorOptions { Timeout = 10000 });
            }

            // Add inline context
            await page.FillAsync("input[placeholder='Enter project / namespace / type id']", "P_E2E");
            // Choose 'Project' in the context kind selector scoped to the add-context area
            await page.SelectOptionAsync("div.add-context select", new[] { "Project" });
            // The Add button may be disabled until the input triggers change; press Tab to commit bind
            await page.PressAsync("input[placeholder='Enter project / namespace / type id']", "Tab");
            // Click the inline Add button specifically
            await page.ClickAsync(".context-add-inline .btn");
            // Wait for the context list item to appear
            await page.WaitForSelectorAsync("li:has-text(\"P_E2E\")", new PageWaitForSelectorOptions { Timeout = 15000 });

            var content = await page.ContentAsync();
            Assert.IsTrue(content.Contains("P_E2E"), "Expected the project id to appear in the engineering context list.");

            // Remove it
            await page.ClickAsync($"text=P_E2E >> text=Remove");
            await page.WaitForSelectorAsync("text=P_E2E", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached, Timeout = 5000 });
            content = await page.ContentAsync();
            Assert.IsFalse(content.Contains("P_E2E"), "Expected the project id to be removed from the engineering context list.");
        }

        [Test]
        public async Task Recommendations_And_Insights_Display()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            // Import repository so the Engineering Model is available for recommendations/insights
            await page.GotoAsync("http://localhost:5005", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForSelectorAsync("input.repo-input", new PageWaitForSelectorOptions { Timeout = 15000 });
            var solutionRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            await page.FillAsync("input.repo-input", solutionRoot);
            await page.PressAsync("input.repo-input", "Tab");
            await page.ClickAsync("text=Import Repository");
            await page.WaitForSelectorAsync("text=Engineering Model", new PageWaitForSelectorOptions { Timeout = 60000 });

            // Begin a task so Recommendations and Insights sections are rendered
            await page.ClickAsync("text=Engineering Workspace");
            await page.WaitForURLAsync("**/workspace", new PageWaitForURLOptions { Timeout = 10000 });
            await page.FillAsync("input[placeholder='Short task title']", "E2E Task");
            await page.FillAsync("textarea[placeholder='What work is being performed?']", "Describe");
            await page.FillAsync("textarea[placeholder='What outcome should this work produce?']", "Goal");
            await page.ClickAsync("text=Begin Current Task");
            await page.WaitForSelectorAsync("text=Complete Task", new PageWaitForSelectorOptions { Timeout = 10000 });

            await page.WaitForSelectorAsync("text=Recommendations", new PageWaitForSelectorOptions { Timeout = 15000 });
            await page.WaitForSelectorAsync("text=Engineering Insights", new PageWaitForSelectorOptions { Timeout = 15000 });

            var content = await page.ContentAsync();
            Assert.IsTrue(content.Contains("Recommendations"), "Expected Recommendations section to be present.");
            Assert.IsTrue(content.Contains("Engineering Insights"), "Expected Engineering Insights section to be present.");
        }
    }
}
