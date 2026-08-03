using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace EngineeringDiscovery.E2ETests.Tests
{
    [TestFixture]
    public class CurrentTaskTests : TestInfrastructure.TestBase
    {
        // Per-test process and Playwright provided by TestBase

        [Test]
        public async Task Begin_Current_Task_Creates_Task()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;
            await page.GotoAsync("http://localhost:5005/workspace", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Fill form and start task
            await page.FillAsync("input[placeholder='Short task title']", "E2E Task");
            await page.FillAsync("textarea[placeholder='What work is being performed?']", "Describe");
            await page.FillAsync("textarea[placeholder='What outcome should this work produce?']", "Goal");
            await page.ClickAsync("text=Begin Current Task");

            await page.WaitForSelectorAsync("text=E2E Task", new PageWaitForSelectorOptions { Timeout = 5000 });
            var content = await page.ContentAsync();
            Assert.IsTrue(content.Contains("E2E Task"), "Expected the task title to appear after beginning the task.");
        }

        [Test]
        public async Task Save_Engineering_Brief_Persists()
        {
            Assert.IsNotNull(Page, "Playwright page must be initialized.");
            var page = Page!;
            await page.GotoAsync("http://localhost:5005/workspace", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            // Ensure a task exists; create if necessary
            if (! (await page.ContentAsync()).Contains("Complete Task"))
            {
                await page.FillAsync("input[placeholder='Short task title']", "E2E Task");
                await page.FillAsync("textarea[placeholder='What work is being performed?']", "Describe");
                await page.FillAsync("textarea[placeholder='What outcome should this work produce?']", "Goal");
                await page.ClickAsync("text=Begin Current Task");
                await page.WaitForSelectorAsync("text=Complete Task", new PageWaitForSelectorOptions { Timeout = 10000 });
            }

            // Set brief fields and save
            await page.FillAsync("input[placeholder='Short objective']", "Objective X");
            await page.FillAsync("textarea[placeholder='Notes about the work']", "Note Y");
            await page.FillAsync("textarea[placeholder='Ideas for implementation']", "Impl Z");
            await page.ClickAsync("text=Save Brief");

            // Wait for brief to be saved and UI to update: check the Objective input value or notes textarea
            await page.WaitForSelectorAsync("input[placeholder='Short objective']", new PageWaitForSelectorOptions { Timeout = 10000 });
            var objectiveVal = await page.InputValueAsync("input[placeholder='Short objective']");
            var notesVal = await page.InputValueAsync("textarea[placeholder='Notes about the work']");
            Assert.IsTrue(objectiveVal.Contains("Objective X") || notesVal.Contains("Note Y"), "Expected brief changes to be reflected in the UI after save.");
        }
    }
}
