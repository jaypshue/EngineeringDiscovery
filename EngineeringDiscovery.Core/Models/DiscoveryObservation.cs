using System;
using System.Collections.Generic;
using System.Text;

namespace EngineeringDiscovery.Core.Models
{
    public class DiscoveryObservation
    {
        public ObservationKind Kind { get; init; }

        public string Project { get; init; } = string.Empty;

        public string? Namespace { get; init; }

        public string? Type { get; init; }

        public string? Member { get; init; }

        public string Description { get; init; } = string.Empty;
    }
}
