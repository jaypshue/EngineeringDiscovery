using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Observations
{
    // Immutable observation record representing any event EngineOS observes.
    public sealed class Observation
    {
        public Observation()
        {
            Id = Guid.NewGuid();
            TimestampUtc = DateTime.UtcNow;
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public Guid Id { get; init; }

        public DateTime TimestampUtc { get; init; }

        // Semantic type, e.g., "UserMessage", "RepoAttached", "DiscoveryCompleted", "BuildResult"
        public string Type { get; init; } = string.Empty;

        // Source identifier, e.g., "User", "RepositoryScanner", "CI", "PackageGenerator"
        public string Source { get; init; } = string.Empty;

        // Optional session id (EngineeringModel.Id) this observation relates to
        public Guid? SessionId { get; init; }

        // Optional payload discriminator
        public string? PayloadType { get; init; }

        // Small structured JSON payload for the observation
        public string PayloadJson { get; init; } = string.Empty;

        // Optional confidence (0.0 - 1.0)
        public double? Confidence { get; init; }

        // Optional correlation id linking related observations
        public string? CorrelationId { get; init; }

        // Optional origin/provenance (file path, commit id, test name)
        public string? Origin { get; init; }

        // Arbitrary metadata for quick queries
        public IReadOnlyDictionary<string, string> Metadata { get; init; }
    }
}
