using System;
using System.Text.Json.Serialization;

namespace EngineeringDiscovery.Web.Services.Persistence
{
    internal sealed class WorkspaceDto
    {
        public Guid Id { get; set; }

        public string SchemaVersion { get; set; } = "1";

        public string RepositoryPath { get; set; } = string.Empty;

        public string? SelectedRole { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime LastModifiedUtc { get; set; }

        // Freshness metadata
        public DateTime? LastBuiltUtc { get; set; }

        public string? RepositoryFingerprint { get; set; }

        public CurrentTaskDto? CurrentTask { get; set; }

        public InvestigationDto? Investigation { get; set; }
    }

    internal sealed class CurrentTaskDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public EngineeringBriefDto? Brief { get; set; }
    }

    internal sealed class EngineeringBriefDto
    {
        public string Objective { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string ImplementationThoughts { get; set; } = string.Empty;
        public DateTime LastUpdatedUtc { get; set; }
    }

    internal sealed class InvestigationDto
    {
        public Guid Id { get; set; }
        public string RepositoryPath { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
