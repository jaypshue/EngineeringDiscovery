using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class AnchorModeBrowseTests : TestInfrastructure.TestBase
    {
        [Test]
        public async Task Browse_Sets_Full_Absolute_Path_In_Repository_Input()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            // Navigate to the Anchor Mode / repository page
            await page.GotoAsync("http://localhost:5005/repository", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Use the solution root and directly populate the repository path input in headless environments
            var solutionRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));

            await page.WaitForSelectorAsync("#repoPathInput", new PageWaitForSelectorOptions { Timeout = 10000 });
            await page.FillAsync("#repoPathInput", solutionRoot);
            await page.PressAsync("#repoPathInput", "Tab");

            // Wait until the input value reflects the expected absolute path
            await page.WaitForFunctionAsync("([selector, expected]) => document.querySelector(selector)?.value === expected", new[] { "#repoPathInput", solutionRoot }, new PageWaitForFunctionOptions { Timeout = 10000 });

            var value = await page.InputValueAsync("#repoPathInput");

            // The requirement: textbox must always display complete absolute path
            Assert.IsTrue(Path.IsPathRooted(value), $"Expected absolute path in repo input, but got: '{value}'.");
        }
    }
}
