using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.CurrentTask;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Service responsible for CurrentTask lifecycle and brief updates.
    /// Must operate against the canonical Workspace provided by WorkspaceState.
    /// </summary>
    public interface ICurrentTaskService
    {
        Task BeginTaskAsync(string title, string description, string goal);
        Task CompleteTaskAsync();
        Task UpdateBriefAsync(Action<EngineeringBrief> update);
        Task AddContextAsync(string kind, string id);
    }
}
