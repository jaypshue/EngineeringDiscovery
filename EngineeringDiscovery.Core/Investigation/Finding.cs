using System;

namespace EngineeringDiscovery.Core.Domain.Investigation
{
    public class Finding
    {
        public Finding(Guid id, FindingType type, string description)
        {
            Id = id == Guid.Empty ? throw new ArgumentException("id must be provided", nameof(id)) : id;
            Type = type;
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public Guid Id { get; }

        public FindingType Type { get; }

        public string Description { get; }
    }
}
