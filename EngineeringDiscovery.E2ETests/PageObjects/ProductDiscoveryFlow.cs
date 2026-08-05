using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace EngineeringDiscovery.E2ETests.PageObjects
{
    public class ProductDiscoveryFlow
    {
        private readonly IPage _page;

        public ProductDiscoveryFlow(IPage page) => _page = page;

        public async Task<bool> StartFromWelcomeAsync(string idea)
        {
            // Start at public root
            await _page.GotoAsync("http://localhost:5005/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Click Free Range link (try text or href)
            try
            {
                if (await _page.QuerySelectorAsync("text=Free Range") != null)
                {
                    await _page.ClickAsync("text=Free Range");
                }
                else
                {
                    await _page.ClickAsync("a[href='/free-range']");
                }
            }
            catch
            {
                // ignore navigation failure; caller will assert page state
            }

            await _page.WaitForTimeoutAsync(500);

            // Try to find an idea input on the Free Range or workspace flows
            // Preferred: data-testid="idea-input" then fallbacks
            var ideaSelectors = new[] { "[data-testid=idea-input]", "input[placeholder*='idea']", "textarea[placeholder*='idea']", "input[placeholder*='Short task title']" };
            foreach (var sel in ideaSelectors)
            {
                try
                {
                    var elem = await _page.QuerySelectorAsync(sel);
                    if (elem != null)
                    {
                        await elem.FillAsync(idea);

                        // Attempt to blur so Blazor processes change
                        await _page.PressAsync(sel, "Tab");

                        // Click start control if present
                        var startCandidates = new[] { "text=Start Building", "text=Begin Product Discovery", "text=Start" };
                        foreach (var c in startCandidates)
                        {
                            try { if (await _page.QuerySelectorAsync(c) != null) { await _page.ClickAsync(c); return true; } } catch { }
                        }

                        return true;
                    }
                }
                catch { }
            }

            // As fallback, navigate to engineos welcome and click Start Building
            try
            {
                await _page.GotoAsync("http://localhost:5005/engineos", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                // Accept any alert
                _page.Dialog += async (_, d) => await d.AcceptAsync();
                if (await _page.QuerySelectorAsync("text=Start Building") != null)
                {
                    await _page.ClickAsync("text=Start Building");
                }
            }
            catch { }

            return true;
        }

        public async Task<(int steps, bool readiness)> RunConversationAsync(string[] answers, int maxSteps = 12)
        {
            int steps = 0;
            bool readiness = false;

            // wait for a question to appear using candidate selectors
            var questionSelectors = new[] { "[data-testid=question]", "#question", ".question-area", "text=Question" };
            var answerSelectors = new[] { "[data-testid=answer-input]", "textarea", "input[type='text']", "input" };
            var continueSelectors = new[] { "[data-testid=continue-button]", "text=Continue", "button:has-text('Continue')" };

            while (steps < maxSteps)
            {
                // Wait for question
                IElementHandle? qElem = null;
                foreach (var sel in questionSelectors)
                {
                    try { qElem = await _page.QuerySelectorAsync(sel); if (qElem != null) break; } catch { }
                }

                if (qElem == null)
                {
                    // attempt to find any text node that looks like a question label
                    try
                    {
                        var content = await _page.ContentAsync();
                        if (!content.Contains("Question") && !content.Contains("question"))
                        {
                            // no visible question UI, break
                            break;
                        }
                    }
                    catch { break; }
                }

                // Ensure not blank
                if (qElem != null)
                {
                    var qText = (await qElem.InnerTextAsync())?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(qText)) throw new Exception("Blank question displayed");
                }

                // Fill answer
                bool answered = false;
                foreach (var sel in answerSelectors)
                {
                    try
                    {
                        var input = await _page.QuerySelectorAsync(sel);
                        if (input != null)
                        {
                            var ans = answers[steps % answers.Length];
                            await input.FillAsync(ans);
                            answered = true;
                            break;
                        }
                    }
                    catch { }
                }

                if (!answered)
                {
                    // nothing to fill, break
                    break;
                }

                // Click continue
                foreach (var csel in continueSelectors)
                {
                    try
                    {
                        var btn = await _page.QuerySelectorAsync(csel);
                        if (btn != null)
                        {
                            await btn.ClickAsync();
                            await _page.WaitForTimeoutAsync(400);
                            break;
                        }
                    }
                    catch { }
                }

                steps++;

                // Check for readiness summary
                try
                {
                    if (await _page.QuerySelectorAsync("[data-testid=readiness-summary]") != null || (await _page.ContentAsync()).Contains("Discovery Readiness Summary"))
                    {
                        readiness = true;
                        break;
                    }
                }
                catch { }
            }

            return (steps, readiness);
        }
    }
}
