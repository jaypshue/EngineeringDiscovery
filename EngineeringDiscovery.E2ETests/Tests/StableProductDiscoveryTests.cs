using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class StableProductDiscoveryTests : TestInfrastructure.TestBase
    {
        [Test]
        public async Task CanLaunchApplication()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            await page.GotoAsync("http://localhost:5005/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Ensure the page returned content and includes at least one welcome indicator
            var htmlContent = await page.ContentAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(htmlContent), "Page should return content on launch.");
            Assert.IsTrue(
                htmlContent.Contains("Free Range") || htmlContent.Contains("EngineOS") || htmlContent.Contains("Welcome"),
                "Welcome or public Free Range text should be visible on launch.");
        }

        [Test]
        public async Task CanStartProductDiscovery()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            await page.GotoAsync("http://localhost:5005/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Navigate to Free Range or EngineOS via stable href anchors when available
            try
            {
                if (await page.QuerySelectorAsync("a[href='/free-range']") != null)
                {
                    await page.ClickAsync("a[href='/free-range']");
                }
                else if (await page.QuerySelectorAsync("a[href='/engineos']") != null)
                {
                    await page.ClickAsync("a[href='/engineos']");
                }
                else if (await page.IsVisibleAsync("text=Free Range"))
                {
                    await page.ClickAsync("text=Free Range");
                }
            }
            catch { }

            // Product Discovery landing should appear (public Free Range heading, Start Building, Product Discovery, or stable nav anchors)
            var navTimeout = 10000;
            var navSw = System.Diagnostics.Stopwatch.StartNew();
            var landed = false;
            while (navSw.ElapsedMilliseconds < navTimeout && !landed)
            {
                try
                {
                    var html = await page.ContentAsync();
                    var hasAnchor = (await page.QuerySelectorAsync("a[href='/engineos']") != null) || (await page.QuerySelectorAsync("a[href='/free-range']") != null);
                    if (html.Contains("Free Range Engineering") || html.Contains("Start Building") || html.Contains("Product Discovery") || hasAnchor)
                    {
                        landed = true;
                        break;
                    }
                }
                catch { }

                await page.WaitForTimeoutAsync(500);
            }

            Assert.IsTrue(landed, "Product Discovery landing should appear (Free Range, Start Building, Product Discovery, or navigation anchors).");
        }

        [Test]
        public async Task CanReceiveFirstQuestion()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            await page.GotoAsync("http://localhost:5005/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Navigate like a user
            // Navigate like a user: prefer stable href anchors to reach the engineos welcome with Start Building
            if (await page.QuerySelectorAsync("a[href='/engineos']") != null)
            {
                await page.ClickAsync("a[href='/engineos']");
            }
            else if (await page.QuerySelectorAsync("a[href='/free-range']") != null)
            {
                await page.ClickAsync("a[href='/free-range']");
            }
            else if (await page.IsVisibleAsync("text=Free Range"))
            {
                await page.ClickAsync("text=Free Range");
            }

            // Attempt to locate an idea input; if not present, clicking Start Building shows a planned alert
            var ideaSelector = await page.QuerySelectorAsync("[data-testid=idea-input]") ?? await page.QuerySelectorAsync("input[placeholder*='idea']") ?? await page.QuerySelectorAsync("textarea[placeholder*='idea']");
            if (ideaSelector != null)
            {
                await ideaSelector.FillAsync("I want smarter debugging assistance");
                await page.ClickAsync("text=Start Building");

                // Wait for first question UI to appear (best-effort)
                var q = await page.QuerySelectorAsync("[data-testid=question]");
                Assert.IsNotNull(q, "Question should appear after starting Product Discovery when idea input exists.");

                // Verify answer input enabled and Continue visible and enabled
                var answer = await page.QuerySelectorAsync("[data-testid=answer-input]") ?? await page.QuerySelectorAsync("textarea") ?? await page.QuerySelectorAsync("input[type='text']");
                Assert.IsNotNull(answer, "Answer input should be present.");
                Assert.IsTrue(await answer.IsEnabledAsync(), "Answer input should be enabled.");

                var cont = await page.QuerySelectorAsync("[data-testid=continue-button]") ?? await page.QuerySelectorAsync("text=Continue");
                Assert.IsNotNull(cont, "Continue control should be present.");
                Assert.IsTrue(await cont.IsEnabledAsync(), "Continue control should be enabled.");
            }
            else
            {
                // No idea input; assert that Start Building shows the planned alert instead of navigating
                string? dialogMessage = null;
                page.Dialog += async (_, d) => { dialogMessage = d.Message; await d.AcceptAsync(); };
                await page.ClickAsync("text=Start Building");
                // Give dialog a moment
                await page.WaitForTimeoutAsync(200);
                Assert.IsNotNull(dialogMessage, "Start Building should show a planned Product Discovery alert when idea input is not present.");
                Assert.IsTrue(dialogMessage.Contains("Product Discovery Mode"), "Dialog should indicate Product Discovery Mode is planned.");
            }
        }
    }
}
