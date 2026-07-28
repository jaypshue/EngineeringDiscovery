using System;

namespace EngineeringDiscovery.Core.Domain.Investigation
{
    public class Finding
    {
        public Finding(Guid id, string description)
        {
            Id = id == Guid.Empty ? throw new ArgumentException("id must be provided", nameof(id)) : id;
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public Guid Id { get; }

        public string Description { get; }
    }
}
