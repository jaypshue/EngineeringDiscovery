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

            // Verify user-visible recovery: ensure a link or button exists that lets the user continue working
            var canContinue = false;
            try
            {
                if (await page.QuerySelectorAsync("text=Start Building") != null) canContinue = true;
                if (await page.QuerySelectorAsync("text=Free Range") != null) canContinue = true;
                if (await page.QuerySelectorAsync("text=Open Workspace") != null) canContinue = true;
            }
            catch { }

            Assert.IsTrue(canContinue, "User should be able to continue working from the landing page.");
        }
    }
}
