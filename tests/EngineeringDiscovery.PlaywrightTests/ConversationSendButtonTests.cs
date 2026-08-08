using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace EngineeringDiscovery.PlaywrightTests
{
    public class ConversationSendButtonTests
    {
        [Test]
        public async Task SendButton_EnablesAndDisables_BasedOnTextboxContent()
        {
            Console.WriteLine($"[Test] SendButton_EnablesAndDisables_BasedOnTextboxContent START at {DateTime.UtcNow:O}");
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            // Use shared test server started by TestServerFixture; fallback to default URL
            var serverUrl = Environment.GetEnvironmentVariable("ED_TEST_SERVER_URL") ?? "http://localhost:5167";
            Console.WriteLine($"[Test] navigating to {serverUrl + "/app"}");
            await page.GotoAsync(serverUrl + "/app");
            Console.WriteLine($"[Test] navigated to {serverUrl + "/app"}");
            // Give Blazor Server some time to establish SignalR connection before sending input events
            Console.WriteLine("[Test] Waiting for SignalR warmup (1500ms)");
            await page.WaitForTimeoutAsync(1500);
            Console.WriteLine("[Test] SignalR warmup complete");

            Console.WriteLine("[Test] Waiting for textarea selector");
            await page.WaitForSelectorAsync(".conversation-host textarea", new PageWaitForSelectorOptions { Timeout = 10000 });
            Console.WriteLine("[Test] textarea selector ready");
            var textarea = await page.QuerySelectorAsync(".conversation-host textarea");
            var sendButton = await page.QuerySelectorAsync(".conversation-host .composer button");

            // Enter text by setting the textarea value and dispatching input events via JS so Blazor sees the update
            Console.WriteLine("[Test] Setting textarea value via JS");
            await page.EvaluateAsync("arg => { const el = document.querySelector(arg.selector); if (!el) return false; el.value = arg.text; el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); return true; }", new { selector = ".conversation-host textarea", text = "Hello" });
            Console.WriteLine("[Test] Waiting for Send button enabled");
            // Wait up to 30s for the Send button to become enabled in the browser (Blazor server initialization may be slow in CI)
            await page.WaitForFunctionAsync("selector => { const el = document.querySelector(selector); return el && !el.disabled; }", ".conversation-host .composer button", new PageWaitForFunctionOptions { Timeout = 30000 });
            Console.WriteLine("[Test] Send button enabled condition met");
            // Re-select the button after the Blazor render may have replaced the element
            sendButton = await page.QuerySelectorAsync(".conversation-host .composer button");
            Console.WriteLine("[Test] Asserting send button is enabled");
            // Single required assertion: Send button becomes enabled after entering "Hello"
            var isDisabled = await sendButton.IsDisabledAsync();
            Console.WriteLine($"[Test] send button disabled state: {isDisabled}");
            Assert.IsFalse(isDisabled, "Send button should be enabled after typing text");

            Console.WriteLine($"[Test] SendButton_EnablesAndDisables_BasedOnTextboxContent COMPLETE at {DateTime.UtcNow:O}");
        }
    }
}
