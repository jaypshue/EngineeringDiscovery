using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using EngineeringDiscovery.E2ETests.PageObjects;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class ProductDiscoverySuiteTests : TestInfrastructure.TestBase
    {
        private record ConversationAsset(string idea, string[] answers);

        private ConversationAsset LoadAsset(string name)
        {
            var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            var assetPath = Path.Combine(root, "EngineeringDiscovery.E2ETests", "ConversationTests", name + ".json");
            var json = File.ReadAllText(assetPath);
            using var doc = JsonDocument.Parse(json);
            var rootEl = doc.RootElement;
            var idea = rootEl.GetProperty("idea").GetString() ?? string.Empty;
            var answers = rootEl.GetProperty("answers").EnumerateArray().Select(el => el.GetString() ?? string.Empty).ToArray();
            return new ConversationAsset(idea, answers);
        }

        private async Task RunScenarioAsync(string assetName)
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            var asset = LoadAsset(assetName);

            var consoleErrors = new System.Collections.Concurrent.ConcurrentBag<string>();
            page.Console += (_, msg) => { if (msg.Type == "error") consoleErrors.Add(msg.Text); };

            var flow = new ProductDiscoveryFlow(page);

            var started = await flow.StartFromWelcomeAsync(asset.idea);
            Assert.IsTrue(started, "Flow should start from welcome.");

            var (steps, readiness) = await flow.RunConversationAsync(asset.answers, maxSteps: 12);

            // Ignore benign resource 404s; fail only on unexpected console errors.
            var severe = consoleErrors.Where(e => !e.Contains("Failed to load resource")).ToArray();
            Assert.IsEmpty(severe, $"Unexpected console errors: {string.Join("; ", severe)}");

            // Assertions: either readiness achieved or multiple steps occurred
            Assert.IsTrue(readiness || steps > 1, "Either readiness should be reached or multiple conversation steps should have progressed.");
        }

        [Test]
        public Task InterviewAssistant() => RunScenarioAsync("InterviewAssistant");

        [Test]
        public Task LearnSignalR() => RunScenarioAsync("SignalRLearning");

        [Test]
        public Task ExistingWorkProject() => RunScenarioAsync("ExistingWork");

        [Test]
        public Task GenericAIProduct() => RunScenarioAsync("GenericAIProduct");
    }
}
