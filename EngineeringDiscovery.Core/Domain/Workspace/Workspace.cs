using System;
using System.IO;
using System.Linq;
// Intentionally avoid bringing conflicting simple type names into scope here.
// Use fully-qualified type names for domain types to prevent ambiguity during compilation.

namespace EngineeringDiscovery.Core.Domain.Workspace
{
    public sealed class Workspace
    {
        public Workspace()
        {
            Id = Guid.NewGuid();
            RepositoryPath = string.Empty;
            Investigation = null;
            CurrentTask = null;
            CurrentActivity = null;
            Iterations = new System.Collections.Generic.List<global::EngineeringDiscovery.Core.Domain.Iteration.EngineeringIteration>();
            // SelectedRole will be set via the property initializer
            CreatedUtc = DateTime.UtcNow;
            LastModifiedUtc = CreatedUtc;
        }

        // Stable identifier to allow future Workspace collections and references
        public Guid Id { get; set; }

        // Schema version for persisted workspace JSON. Increment when changing the persisted shape.
        public string SchemaVersion { get; set; } = "1";

        public string RepositoryPath { get; set; }

        // Investigation may be null until discovery completes
        public global::EngineeringDiscovery.Core.Domain.Investigation.Investigation? Investigation { get; set; }

        // CurrentTask is optional; the workspace may start without an active task
        public global::EngineeringDiscovery.Core.Domain.CurrentTask.CurrentTask? CurrentTask { get; set; }

        public global::EngineeringDiscovery.Core.Domain.Models.EngineeringRole SelectedRole { get; set; } = global::EngineeringDiscovery.Core.Domain.Models.EngineeringRole.CurrentTask;

        public DateTime CreatedUtc { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        // Support multiple imported repositories attached to this workspace.
        public System.Collections.Generic.List<ImportedRepository> ImportedRepositories { get; set; } = new();

        // ED-300: Activity support (single active activity for initial scope)
        public global::EngineeringDiscovery.Core.Domain.Activity.EngineeringActivity? CurrentActivity { get; set; }

        // Lightweight iteration history for small engineering loops (v1)
        public System.Collections.Generic.List<global::EngineeringDiscovery.Core.Domain.Iteration.EngineeringIteration> Iterations { get; set; }

        // Authoritative Engineering State (project-level)
        public global::EngineeringDiscovery.Core.Domain.ProjectState.ProjectState? ProjectState { get; set; }

        // Internal EngineOS understanding contexts. The selected folder remains the user's
        // software context; these records persist what EngineOS understands about it.
        public System.Collections.Generic.List<Project> Projects { get; set; } = new();
        public Guid? ActiveProjectId { get; set; }

        /// <summary>
        /// Convenience accessor: returns the active project's state, or null if no project is active.
        /// This is the primary read path for services that need the current project's state.
        /// </summary>
        public global::EngineeringDiscovery.Core.Domain.ProjectState.ProjectState? ActiveProjectState
        {
            get
            {
                if (ActiveProjectId == null || Projects == null || Projects.Count == 0) return null;
                var project = Projects.Find(p => p.Id == ActiveProjectId);
                return project?.State;
            }
        }

        /// <summary>
        /// Convenience: returns the active Project entity, or null.
        /// </summary>
        public Project? ActiveProject
        {
            get
            {
                if (ActiveProjectId == null || Projects == null || Projects.Count == 0) return null;
                return Projects.Find(p => p.Id == ActiveProjectId);
            }
        }

        /// <summary>
        /// Finds the internal EngineOS project associated with a selected software folder.
        /// ImportedRepository.ProjectId is authoritative when present; RepositoryPaths is
        /// retained as a legacy fallback for workspaces created before the explicit link.
        /// </summary>
        public Project? FindProjectForRepositoryPath(string? repositoryPath)
        {
            var normalizedPath = NormalizeRepositoryPath(repositoryPath);
            if (string.IsNullOrWhiteSpace(normalizedPath) || Projects is null || Projects.Count == 0)
            {
                return null;
            }

            var linkedProjectIds = (ImportedRepositories ?? new())
                .Where(repository => PathsEqual(repository.RepositoryPath, normalizedPath) && repository.ProjectId.HasValue)
                .Select(repository => repository.ProjectId!.Value)
                .Distinct()
                .ToList();

            if (linkedProjectIds.Count == 1)
            {
                var linkedProject = Projects.FirstOrDefault(project => project.Id == linkedProjectIds[0]);
                if (linkedProject is not null)
                {
                    return linkedProject;
                }
            }

            var pathMatches = Projects
                .Where(project => (project.RepositoryPaths ?? new()).Any(path => PathsEqual(path, normalizedPath)))
                .ToList();

            return pathMatches.Count == 1 ? pathMatches[0] : null;
        }

        /// <summary>
        /// Finds the imported folder record associated with an internal EngineOS project.
        /// </summary>
        public ImportedRepository? FindImportedRepositoryForProject(Guid projectId, string? repositoryPath = null)
        {
            var normalizedPath = NormalizeRepositoryPath(repositoryPath);
            return (ImportedRepositories ?? new()).FirstOrDefault(repository =>
                repository.ProjectId == projectId &&
                (string.IsNullOrWhiteSpace(normalizedPath) || PathsEqual(repository.RepositoryPath, normalizedPath)));
        }

        /// <summary>
        /// Establishes explicit folder-to-project links for newly created and legacy workspaces.
        /// Ambiguous legacy matches are intentionally left unlinked rather than guessed.
        /// </summary>
        public void EstablishRepositoryProjectLinks()
        {
            Projects ??= new System.Collections.Generic.List<Project>();
            ImportedRepositories ??= new System.Collections.Generic.List<ImportedRepository>();

            foreach (var importedRepository in ImportedRepositories)
            {
                if (importedRepository.ProjectId.HasValue && Projects.All(project => project.Id != importedRepository.ProjectId.Value))
                {
                    importedRepository.ProjectId = null;
                }

                if (!importedRepository.ProjectId.HasValue)
                {
                    var matches = Projects
                        .Where(project => (project.RepositoryPaths ?? new()).Any(path => PathsEqual(path, importedRepository.RepositoryPath)))
                        .ToList();
                    if (matches.Count == 1)
                    {
                        importedRepository.ProjectId = matches[0].Id;
                    }
                }

                if (importedRepository.ProjectId.HasValue)
                {
                    var project = Projects.FirstOrDefault(candidate => candidate.Id == importedRepository.ProjectId.Value);
                    if (project is not null && !string.IsNullOrWhiteSpace(importedRepository.RepositoryPath))
                    {
                        project.RepositoryPaths ??= new System.Collections.Generic.List<string>();
                        if (!project.RepositoryPaths.Any(path => PathsEqual(path, importedRepository.RepositoryPath)))
                        {
                            project.RepositoryPaths.Add(importedRepository.RepositoryPath);
                        }
                    }
                }
            }
        }

        public static string NormalizeRepositoryPath(string? repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath)) return string.Empty;

            try
            {
                var fullPath = Path.GetFullPath(repositoryPath.Trim());
                var root = Path.GetPathRoot(fullPath);
                return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                    ? fullPath
                    : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return repositoryPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        public static bool PathsEqual(string? left, string? right)
        {
            var normalizedLeft = NormalizeRepositoryPath(left);
            var normalizedRight = NormalizeRepositoryPath(right);
            return !string.IsNullOrWhiteSpace(normalizedLeft)
                && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        // Freshness metadata
        // The time the Engineering Model (Investigation) was last built for this workspace
        public DateTime? LastBuiltUtc { get; set; }

        // Lightweight repository fingerprint used to detect potential repository changes
        // Version 1 uses a simple string (e.g., latest file write timestamp) and can be extended later
        public string? RepositoryFingerprint { get; set; }

        public void Touch()
        {
            LastModifiedUtc = DateTime.UtcNow;
        }

        public void SetFreshness(DateTime builtUtc, string? fingerprint)
        {
            LastBuiltUtc = builtUtc;
            RepositoryFingerprint = fingerprint;
            Touch();
        }

        public bool IsEmpty() => (string.IsNullOrWhiteSpace(RepositoryPath) && (ImportedRepositories == null || ImportedRepositories.Count == 0)) && Investigation is null;
    }

    public sealed class ImportedRepository
    {
        public ImportedRepository()
        {
            RepositoryPath = string.Empty;
            CreatedUtc = DateTime.UtcNow;
        }

        public string RepositoryPath { get; set; }

        /// <summary>
        /// The internal EngineOS project containing durable understanding for this folder.
        /// Nullable so workspaces persisted before the explicit relationship can migrate safely.
        /// </summary>
        public Guid? ProjectId { get; set; }

        public DateTime CreatedUtc { get; set; }
        public DateTime? LastBuiltUtc { get; set; }
        public string? RepositoryFingerprint { get; set; }
        public global::EngineeringDiscovery.Core.Domain.Investigation.Investigation? Investigation { get; set; }
    }
}
