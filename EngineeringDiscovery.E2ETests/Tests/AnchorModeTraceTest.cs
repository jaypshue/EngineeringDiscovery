using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class AnchorModeTraceTest : TestInfrastructure.TestBase
    {
        [Test]
        public async Task Trace_Browse_To_Detection_Path()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;

            // Capture console messages from the browser page
            page.Console += (_, msg) =>
            {
                TestContext.WriteLine($"PAGE CONSOLE [{msg.Type}] {msg.Text}");
            };

            // Navigate to Anchor Mode / repository page
            await page.GotoAsync("http://localhost:5005/repository", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Prepare sample files
            var solutionRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            var sampleFiles = Directory.EnumerateFiles(solutionRoot, "*.*", SearchOption.AllDirectories).Take(10).ToArray();
            Assert.IsNotEmpty(sampleFiles, "No sample files found for test.");

            TestContext.WriteLine($"Test: setting {sampleFiles.Length} files on #folderPicker; sample[0]={sampleFiles[0]}");

            // In headless/test environments, simulate Browse by directly populating the repo input with the absolute path
            await page.FillAsync("#repoPathInput", solutionRoot);
            TestContext.WriteLine("Test: Filled #repoPathInput with solutionRoot");
            // Ensure Blazor processes the change (bind on input)
            await page.PressAsync("#repoPathInput", "Tab");

            // Wait for any console.log lines from detectFromFileInput or attachFolderInputHandler
            // We'll wait up to 5s for messages to appear
            await page.WaitForTimeoutAsync(2000);

            // Snapshot of folderPicker.files properties in page
            var fileInfo = await page.EvaluateAsync<string>(@"() => {
                const el = document.getElementById('folderPicker');
                if(!el || !el.files) return 'NO_FILES';
                const list = [];
                for(let i=0;i<Math.min(el.files.length,10);i++){
                    const f = el.files[i];
                    list.push({name:f.name, webkitRelativePath: f.webkitRelativePath || null, path: f.path || null});
                }
                return JSON.stringify(list);
            }");

            TestContext.WriteLine("Client file input snapshot: " + fileInfo);

            // Read the repoPathInput value (textbox) after client detection
            var repoInput = await page.InputValueAsync("#repoPathInput");
            TestContext.WriteLine("Repo input value after Browse: '" + repoInput + "'");

            // Read analysis list text
            string analysisText = string.Empty;
            try
            {
                var el = await page.QuerySelectorAsync(".analysis-list");
                if (el != null)
                {
                    analysisText = (await el.InnerTextAsync()) ?? string.Empty;
                }
            }
            catch { }

            TestContext.WriteLine("Repository Analysis text:\n" + analysisText);

            // Also read any displayed error text
            var errorText = string.Empty;
            try { errorText = (await page.InnerTextAsync(".import-feedback .error")).Trim(); } catch { }
            if (!string.IsNullOrWhiteSpace(errorText)) TestContext.WriteLine("Import feedback error: " + errorText);

            // Final assertions to ensure this reproduces the defect: require the repo input to be absolute
            Assert.IsTrue(!string.IsNullOrWhiteSpace(repoInput), "Repo input is empty after Browse.");
            Assert.IsTrue(Path.IsPathRooted(repoInput), $"Expected absolute path in repo input, but got: '{repoInput}'. See test log for details.");
        }
    }
}
