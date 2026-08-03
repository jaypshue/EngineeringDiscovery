using System.Threading.Tasks;
using Microsoft.Playwright;

namespace EngineeringDiscovery.E2ETests.PageObjects
{
    public class WorkspacePage
    {
        private readonly IPage _page;

        public WorkspacePage(IPage page) { _page = page; }

        public async Task GoToAsync() => await _page.GotoAsync("/workspace");

        public async Task ImportRepositoryAsync(string path)
        {
            // Navigate via the unified startup page to the repository import surface
            await _page.GotoAsync("/", new() { WaitUntil = WaitUntilState.NetworkIdle });
            await _page.ClickAsync("text=Import Repository");
            await _page.WaitForSelectorAsync("input.repo-input", new PageWaitForSelectorOptions { Timeout = 15000 });
            await _page.FillAsync("input.repo-input", path);
            await _page.PressAsync("input.repo-input", "Tab");
            await _page.ClickAsync("text=Import Repository");
            await _page.WaitForSelectorAsync("text=Engineering Model", new PageWaitForSelectorOptions { Timeout = 60000 });
        }

        public async Task BeginCurrentTaskAsync(string title, string description, string goal)
        {
            await _page.FillAsync("input[placeholder='Short task title']", title);
            await _page.FillAsync("textarea[placeholder='What work is being performed?']", description);
            await _page.FillAsync("textarea[placeholder='What outcome should this work produce?']", goal);
            await _page.ClickAsync("text=Begin Current Task");
            await _page.WaitForSelectorAsync($"text={title}", new PageWaitForSelectorOptions { Timeout = 5000 });
        }

        public async Task SaveBriefAsync(string objective, string notes, string implementation)
        {
            await _page.FillAsync("input[placeholder='Short objective']", objective);
            await _page.FillAsync("textarea[placeholder='Notes about the work']", notes);
            await _page.FillAsync("textarea[placeholder='Ideas for implementation']", implementation);
            await _page.ClickAsync("text=Save Brief");
            await _page.WaitForSelectorAsync($"text={objective}", new PageWaitForSelectorOptions { Timeout = 5000 });
        }

        public async Task AddContextInlineAsync(string id, string kind = "Project")
        {
            await _page.FillAsync("input[placeholder='Enter project / namespace / type id']", id);
            await _page.SelectOptionAsync("select", new[] { kind });
            await _page.ClickAsync("text=Add");
            await _page.WaitForSelectorAsync($"text={id}", new PageWaitForSelectorOptions { Timeout = 5000 });
        }

        public async Task RemoveContextAsync(string id)
        {
            await _page.ClickAsync($"text={id} >> text=Remove");
            await _page.WaitForSelectorAsync($"text={id}", new PageWaitForSelectorOptions { State = WaitForSelectorState.Detached, Timeout = 5000 });
        }

        public async Task<bool> RecommendationsPresentAsync()
        {
            var content = await _page.ContentAsync();
            return content.Contains("Recommendations");
        }

        public async Task<bool> InsightsPresentAsync()
        {
            var content = await _page.ContentAsync();
            return content.Contains("Engineering Insights");
        }
    }
}
