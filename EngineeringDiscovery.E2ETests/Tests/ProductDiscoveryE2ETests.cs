using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    [Ignore("ED-307 Reset: replaced by StableProductDiscoveryTests")]
    public class ProductDiscoveryE2ETests : TestInfrastructure.TestBase
    {
        private readonly string[] cannedAnswers = new[]
        {
            "Software developers",
            "Technical interviews",
            "During the interview",
            "Live transcript",
            "Technical guidance",
            "Desktop application",
            "Learning and interview success"
        };

        [Test]
        public async Task ProductDiscovery_Workflow_CompletesOrProgressesWithoutErrors()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            // Capture console errors
            var consoleErrors = new System.Collections.Concurrent.ConcurrentBag<string>();
            page.Console += (_, msg) =>
            {
                if (msg.Type == "error") consoleErrors.Add(msg.Text);
            };

            // Navigate to public welcome and click the Free Range link
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.ClickAsync("text=Free Range", new PageClickOptions { Timeout = 5000 });
            // Verify Free Range page loaded
            await page.WaitForSelectorAsync("text=Free Range Engineering", new PageWaitForSelectorOptions { Timeout = 5000 });

            // Go to the EngineOS workspace welcome where Start Building exists
            await page.GotoAsync("/engineos", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Handle any alert that may be shown when clicking Start Building
            page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

            // Click Start Building and ensure the app remains responsive (some flows show an alert placeholder)
            await page.ClickAsync("text=Start Building", new PageClickOptions { Timeout = 5000 });

            // Navigate to workspace to proceed with discovery flow
            await page.GotoAsync("/workspace", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // If the Product Discovery entry exists, start it. Otherwise, ensure workspace loaded.
            var content = await page.ContentAsync();
            Assert.IsTrue(content.Length > 0, "Page content should be present after navigation.");

            // Attempt to find a question area or a begin/discovery control and interact if present.
            // The UI for product discovery may vary; this test treats AI as non-deterministic and focuses on workflow.

            // Prefer explicit selectors if implemented; try several fallbacks.
            var questionSelectorCandidates = new[]
            {
                "#question", // explicit id
                ".question-area", // class
                "text=Question", // labeled text
                "textarea[placeholder='Your answer']",
                "input[placeholder='Your answer']"
            };

            // Try to find a Continue button as well
            var continueSelectors = new[] { "text=Continue", "button:has-text('Continue')", "button.continue" };

            // If a question appears, iterate through canned answers
            var questionFound = false;
            for (int i = 0; i < cannedAnswers.Length; i++)
            {
                // Look for any of the question selectors
                IElementHandle? qElem = null;
                foreach (var sel in questionSelectorCandidates)
                {
                    try
                    {
                        var handle = await page.QuerySelectorAsync(sel);
                        if (handle != null)
                        {
                            qElem = handle;
                            break;
                        }
                    }
                    catch { }
                }

                if (qElem == null)
                {
                    // No obvious question UI found; break early but assert that the app did not crash
                    break;
                }

                questionFound = true;

                // Ensure the question text isn't blank
                var qText = (await qElem.InnerTextAsync()).Trim();
                Assert.IsFalse(string.IsNullOrWhiteSpace(qText), "Question text should not be blank.");

                // Fill an answer into the first available input/textarea
                var answered = false;
                foreach (var sel in new[] { "textarea", "input[type='text']", "input" })
                {
                    try
                    {
                        var input = await page.QuerySelectorAsync(sel);
                        if (input != null)
                        {
                            await input.FillAsync(cannedAnswers[i]);
                            answered = true;
                            break;
                        }
                    }
                    catch { }
                }

                // If no input found, skip trying to continue
                if (!answered) break;

                // Click Continue if present
                foreach (var csel in continueSelectors)
                {
                    try
                    {
                        var btn = await page.QuerySelectorAsync(csel);
                        if (btn != null)
                        {
                            await btn.ClickAsync();
                            // wait a short time for UI to update
                            await page.WaitForTimeoutAsync(500);
                            break;
                        }
                    }
                    catch { }
                }
            }

            // Final assertions: no severe console errors were emitted and page remained responsive
            var severe = consoleErrors.Where(e => !e.Contains("Failed to load resource")).ToArray();
            Assert.IsEmpty(severe, $"Unexpected console errors: {string.Join("; ", severe)}");
            Assert.IsTrue(content.Contains("Free Range") || questionFound, "Either Free Range content should be visible or a question should have been found to exercise discovery.");
        }
    }
}
