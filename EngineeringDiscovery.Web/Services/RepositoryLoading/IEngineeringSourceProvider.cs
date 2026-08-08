using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    /// <summary>
    /// Provider contract for extracting engineering source knowledge from a repository.
    /// Implementations must produce language- or artifact-neutral CompilationContext objects
    /// and must not expose parser/compiler-specific types.
    /// </summary>
    internal interface IEngineeringSourceProvider
    {
        /// <summary>
        /// Heuristic: can this provider analyze the given repository root?
        /// </summary>
        bool CanLoad(string repositoryRoot);

        /// <summary>
        /// Load provider-neutral CompilationContext objects from the repository root.
        /// </summary>
        IReadOnlyList<CompilationContext> Load(string repositoryRoot);
    }
}
