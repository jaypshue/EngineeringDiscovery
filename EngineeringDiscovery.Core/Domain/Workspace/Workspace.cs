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

        public void Touch()
        {
            LastModifiedUtc = DateTime.UtcNow;
        }

        public bool IsEmpty() => string.IsNullOrWhiteSpace(RepositoryPath) && Investigation is null;
    }
}
