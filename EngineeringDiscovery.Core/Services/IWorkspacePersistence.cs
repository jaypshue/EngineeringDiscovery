using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Workspace;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Persistence abstraction for loading and saving the canonical Workspace.
    /// Implementations perform file I/O or other durable storage operations outside of WorkspaceState.
    /// </summary>
    public interface IWorkspacePersistence
    {
        /// <summary>
        /// Load the persisted workspace if present, otherwise null.
        /// </summary>
        Task<Workspace?> LoadAsync();

        /// <summary>
        /// Persist the provided workspace.
        /// </summary>
        Task SaveAsync(Workspace? workspace);
    }
}
