using System;

namespace EngineeringDiscovery.Core.Models
{
    /// <summary>
    /// A lightweight representation of an engineering artifact produced by an Investigation.
    /// This is intentionally minimal: an identifier, a short title, a description, and a creation timestamp.
    /// The existing domain types (e.g., Finding) are not replaced by this type; this class provides a
    /// shared vocabulary for future evolution.
    /// </summary>
    public sealed class InvestigationArtifact
    {
        public InvestigationArtifact(Guid id, string title, string description)
        {
            Id = id == Guid.Empty ? throw new ArgumentException("id must be provided", nameof(id)) : id;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? string.Empty;
            CreatedOn = DateTime.UtcNow;
        }

        public Guid Id { get; }

        public string Title { get; }

        public string Description { get; }

        public DateTime CreatedOn { get; }
    }
}
