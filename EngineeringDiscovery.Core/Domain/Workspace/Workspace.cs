using System;
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

        public DateTime CreatedUtc { get; private set; }

        public DateTime LastModifiedUtc { get; private set; }

        // Support multiple imported repositories attached to this workspace.
        public System.Collections.Generic.List<ImportedRepository> ImportedRepositories { get; set; } = new();

        // ED-300: Activity support (single active activity for initial scope)
        public global::EngineeringDiscovery.Core.Domain.Activity.EngineeringActivity? CurrentActivity { get; set; }

        // Lightweight iteration history for small engineering loops (v1)
        public System.Collections.Generic.List<global::EngineeringDiscovery.Core.Domain.Iteration.EngineeringIteration> Iterations { get; set; }

        // Freshness metadata
        // The time the Engineering Model (Investigation) was last built for this workspace
        public DateTime? LastBuiltUtc { get; private set; }

        // Lightweight repository fingerprint used to detect potential repository changes
        // Version 1 uses a simple string (e.g., latest file write timestamp) and can be extended later
        public string? RepositoryFingerprint { get; private set; }

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
        public DateTime CreatedUtc { get; set; }
        public DateTime? LastBuiltUtc { get; set; }
        public string? RepositoryFingerprint { get; set; }
        public global::EngineeringDiscovery.Core.Domain.Investigation.Investigation? Investigation { get; set; }
    }
}
