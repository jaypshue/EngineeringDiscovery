using System;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.ProjectState;

namespace EngineeringDiscovery.Core.Domain.Workspace
{
    /// <summary>
    /// Internal EngineOS engineering-understanding state associated with software folders.
    /// This is not a user-managed project-management artifact. Multiple software folders
    /// may be associated with an understanding context when the domain requires it.
    /// </summary>
    public sealed class Project
    {
        public Project()
        {
            Id = Guid.NewGuid();
            State = new ProjectState.ProjectState();
            RepositoryPaths = new List<string>();
            CreatedUtc = DateTime.UtcNow;
            LastOpenedUtc = CreatedUtc;
        }

        public Guid Id { get; set; }

        /// <summary>
        /// The project's engineering state (identity, lifecycle, resume point, engagements, etc.)
        /// </summary>
        public ProjectState.ProjectState State { get; set; }

        /// <summary>
        /// Repository/folder paths associated with this project.
        /// </summary>
        public List<string> RepositoryPaths { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime LastOpenedUtc { get; set; }

        /// <summary>
        /// Convenience: the project name from its state identity.
        /// </summary>
        public string Name => State?.Identity?.Name ?? "(unnamed)";
    }
}
