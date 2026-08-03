using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class AnchorModeSelectionContractTests : TestInfrastructure.TestBase
    {
        [Test]
        public async Task Browse_Keeps_Exact_Path_And_Detection_Uses_Selected_Repo()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            // Navigate to the Anchor Mode / repository page
            await page.GotoAsync("http://localhost:5005/repository", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Prepare test repos
            var root = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "EngineeringDiscovery.E2ETests"));
            var javaRepo = Path.GetFullPath(Path.Combine(root, "TestRepos", "JavaRepo"));
            var dotnetRepo = Path.GetFullPath(Path.Combine(root, "TestRepos", "DotNetRepo"));

            // 1) Browse Java repo via direct input (works in headless reliably)
            await page.WaitForSelectorAsync("#repoPathInput", new PageWaitForSelectorOptions { Timeout = 10000 });
            await page.FillAsync("#repoPathInput", javaRepo);
            await page.PressAsync("#repoPathInput", "Tab");
            await page.WaitForFunctionAsync("([selector, expected]) => document.querySelector(selector)?.value === expected", new[] { "#repoPathInput", javaRepo }, new PageWaitForFunctionOptions { Timeout = 5000 });
            var value1 = await page.InputValueAsync("#repoPathInput");
            Assert.AreEqual(javaRepo, value1, "Repository Folder must exactly match the selected Java repo path.");

            // Wait for detection to report Java evidence
            await page.WaitForFunctionAsync("() => document.querySelector('.import-feedback') && document.querySelector('.import-feedback').innerText.indexOf('Java') >= 0", new PageWaitForFunctionOptions { Timeout = 5000 });
            var feedback1 = await page.InnerTextAsync(".import-feedback");
            StringAssert.Contains("Java", feedback1, "Repository Analysis must indicate Java evidence for the selected Java repo.");

            // 2) Browse .NET repo
            await page.FillAsync("#repoPathInput", dotnetRepo);
            await page.PressAsync("#repoPathInput", "Tab");
            await page.WaitForFunctionAsync("([selector, expected]) => document.querySelector(selector)?.value === expected", new[] { "#repoPathInput", dotnetRepo }, new PageWaitForFunctionOptions { Timeout = 5000 });
            var value2 = await page.InputValueAsync("#repoPathInput");
            Assert.AreEqual(dotnetRepo, value2, "Repository Folder must exactly match the selected .NET repo path.");

            await page.WaitForFunctionAsync("() => document.querySelector('.import-feedback') && document.querySelector('.import-feedback').innerText.indexOf('.NET') >= 0", new PageWaitForFunctionOptions { Timeout = 5000 });
            var feedback2 = await page.InnerTextAsync(".import-feedback");
            StringAssert.Contains(".NET", feedback2, "Repository Analysis must indicate .NET evidence for the selected .NET repo.");
        }
    }
}
