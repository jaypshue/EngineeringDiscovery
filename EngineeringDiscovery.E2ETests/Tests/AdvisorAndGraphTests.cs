using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class AdvisorAndGraphTests : TestInfrastructure.TestBase
    {
        // Per-test process and Playwright provided by TestBase

        [Test]
        public async Task Advisor_Can_Answer_Summary()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            // Ensure repository imported and current task exists so the advisor has data to answer
            await page.GotoAsync("http://localhost:5005", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForSelectorAsync("input.repo-input", new PageWaitForSelectorOptions { Timeout = 15000 });
            var solutionRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            await page.FillAsync("input.repo-input", solutionRoot);
            await page.PressAsync("input.repo-input", "Tab");
            await page.ClickAsync("text=Import Repository");
            await page.WaitForSelectorAsync("text=Engineering Model", new PageWaitForSelectorOptions { Timeout = 60000 });

            await page.ClickAsync("text=Engineering Workspace");
            await page.WaitForURLAsync("**/workspace", new PageWaitForURLOptions { Timeout = 10000 });
            if (!(await page.ContentAsync()).Contains("Complete Task"))
            {
                await page.FillAsync("input[placeholder='Short task title']", "E2E Task");
                await page.FillAsync("textarea[placeholder='What work is being performed?']", "Describe");
                await page.FillAsync("textarea[placeholder='What outcome should this work produce?']", "Goal");
                await page.ClickAsync("text=Begin Current Task");
                await page.WaitForSelectorAsync("text=Complete Task", new PageWaitForSelectorOptions { Timeout = 10000 });
            }

            await page.FillAsync("input[placeholder='e.g., Why was type X recommended?']", "summarize my current task");
            await page.ClickAsync("text=Ask");
            await page.WaitForSelectorAsync("text=Current Task", new PageWaitForSelectorOptions { Timeout = 15000 });

            var content = await page.ContentAsync();
            Assert.IsTrue(content.Contains("Current Task") || content.Contains("Working Set"), "Expected advisor response to include current task or working set evidence.");
        }

        [Test]
        public async Task Relationship_Graph_Loads()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;
            await page.GotoAsync("http://localhost:5005/graph", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Wait for the graph container element
            await page.WaitForSelectorAsync("#cy", new PageWaitForSelectorOptions { Timeout = 5000 });

            var content = await page.ContentAsync();
            Assert.IsTrue(content.Contains("id=\"cy\"") || content.Contains("graph-workspace"), "Expected graph container to be present on /graph page.");
        }
    }
}
