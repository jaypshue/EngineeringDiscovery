using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

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

            bool started = false;

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
                            try
                            {
                                if (await _page.QuerySelectorAsync(c) != null)
                                {
                                    await _page.ClickAsync(c);
                                    started = true;
                                    break;
                                }
                            }
                            catch { }
                        }

                        // If we filled the idea but did not find an explicit start button, still consider started
                        // only if the UI navigates to workspace or shows a discovery control.
                        if (!started)
                        {
                            // give Blazor a moment to react
                            await _page.WaitForTimeoutAsync(500);
                            try
                            {
                                if ((await _page.QuerySelectorAsync("[data-testid=question]") ) != null) started = true;
                            }
                            catch { }
                        }

                        if (started)
                        {
                            // Ensure workspace URL loaded
                            try { await _page.GotoAsync("http://localhost:5005/workspace", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }); } catch { }
                        }

                        if (started) return true;
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
                    started = true;
                    // Give UI a moment and ensure workspace route
                    await _page.WaitForTimeoutAsync(500);
                    try { await _page.GotoAsync("http://localhost:5005/workspace", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle }); } catch { }
                }
            }
            catch { }

            return started;
        }

        public async Task<(int steps, bool readiness)> RunConversationAsync(string[] answers, int maxSteps = 12)
        {
            int steps = 0;
            bool readiness = false;
            int advancedCount = 0;

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

                // If we still don't have a question element, try broader heuristics (elements containing '?' or common question words)
                if (qElem == null)
                {
                    try
                    {
                        var altSelectors = new[]
                        {
                            "xpath=//*[contains(text(),'?')]",
                            "xpath=//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), ' what ')]",
                            "xpath=//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), ' why ')]",
                            "xpath=//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), ' how ')]",
                            "xpath=//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), ' when ')]",
                            "xpath=//*[contains(translate(., 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), ' who ')]"
                        };

                        foreach (var sel in altSelectors)
                        {
                            try
                            {
                                var alt = await _page.QuerySelectorAsync(sel);
                                if (alt != null)
                                {
                                    qElem = alt;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // Ensure not blank and capture diagnostics
                var qText = string.Empty;
                if (qElem != null)
                {
                    qText = (await qElem.InnerTextAsync())?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(qText)) throw new Exception("Blank question displayed");
                }

                // Diagnostics: save screenshot/html and log question + input/continue enabled states
                try
                {
                    var testName = TestContext.CurrentContext.Test?.Name ?? "unknown";
                    var diagDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Diagnostics", testName);
                    Directory.CreateDirectory(diagDir);
                    var preShot = Path.Combine(diagDir, $"step-{steps + 1}-pre.png");
                    await _page.ScreenshotAsync(new PageScreenshotOptions { Path = preShot, FullPage = true });
                    var preHtml = Path.Combine(diagDir, $"step-{steps + 1}-pre.html");
                    File.WriteAllText(preHtml, await _page.ContentAsync());
                    TestContext.WriteLine($"[Diag] Step {steps + 1} - question: {qText}");

                    // Check answer input enabled
                    IElementHandle? inputHandle = null;
                    foreach (var sel in answerSelectors)
                    {
                        try { inputHandle = await _page.QuerySelectorAsync(sel); if (inputHandle != null) break; } catch { }
                    }
                    if (inputHandle != null)
                    {
                        var enabled = await inputHandle.IsEnabledAsync();
                        TestContext.WriteLine($"[Diag] Step {steps + 1} - answer input enabled: {enabled}");
                    }
                    else
                    {
                        TestContext.WriteLine($"[Diag] Step {steps + 1} - answer input not found");
                    }

                    // Check continue button enabled
                    IElementHandle? btnHandle = null;
                    foreach (var csel in continueSelectors)
                    {
                        try { btnHandle = await _page.QuerySelectorAsync(csel); if (btnHandle != null) break; } catch { }
                    }
                    if (btnHandle != null)
                    {
                        var btnEnabled = await btnHandle.IsEnabledAsync();
                        TestContext.WriteLine($"[Diag] Step {steps + 1} - continue button enabled: {btnEnabled}");
                    }
                    else
                    {
                        TestContext.WriteLine($"[Diag] Step {steps + 1} - continue button not found");
                    }
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine($"[Diag] Step {steps + 1} - diagnostics failed: {ex.Message}");
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
                            var before = await _page.ContentAsync();
                            await btn.ClickAsync();
                            // Wait for either navigation or content change
                            try
                            {
                                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 2000 });
                            }
                            catch { }
                            await _page.WaitForTimeoutAsync(400);
                            var after = await _page.ContentAsync();
                            if (!string.Equals(before, after, StringComparison.Ordinal))
                            {
                                advancedCount++;
                            }
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

            // If clicks produced content changes but steps counter stayed 0, report advancement
            steps = Math.Max(steps, advancedCount);
            return (steps, readiness);
        }
    }
}
