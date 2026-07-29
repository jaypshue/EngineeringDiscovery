using System;

namespace EngineeringDiscovery.Core.Domain.Models
{
    public enum InvestigationStatus
    {
        Draft,
        Active,
        Closed
    }

    public class Investigation
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public InvestigationStatus Status { get; set; }

        public DateTime CreatedOn { get; set; }

        public string? Description { get; set; }
    }
}
