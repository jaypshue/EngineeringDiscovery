using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Workspace;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// In-memory persistence for unit tests. Keeps a copy of the workspace in memory.
    /// </summary>
    public sealed class InMemoryWorkspacePersistence : IWorkspacePersistence
    {
        private Workspace? _store;

        public Task<Workspace?> LoadAsync() => Task.FromResult(_store);

        public Task SaveAsync(Workspace? workspace)
        {
            // For test isolation we store the instance directly. Tests that require deep cloning should
            // create their own copies as needed. Attempting JSON-based clone loses data for some
            // get-only collection properties, so avoid serialization here.
            _store = workspace;
            return Task.CompletedTask;
        }
    }
}
