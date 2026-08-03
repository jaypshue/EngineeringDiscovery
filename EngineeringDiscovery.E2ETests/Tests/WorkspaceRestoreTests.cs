using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using EngineeringDiscovery.E2ETests.TestInfrastructure;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class WorkspaceRestoreTests : TestBase
    {
        [Test]
        public async Task Workspace_Restores_OnStart()
        {
            // Page is initialized in TestBase.SetUp
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;
            await page.GotoAsync("http://localhost:5005", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            var content = await page.ContentAsync();
            Assert.IsTrue(content.Contains("Dashboard") || content.Contains("Engineering Workspace"), "Expected top-level workspace UI to be present.");
        }
    }
}
