using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    /// <summary>
    /// Agent-neutral profile for a registered coding worker.
    /// Structural placeholder for future routing logic.
    /// </summary>
    public enum WorkerCapabilityTier
    {
        Fast,
        Standard,
        Deep
    }

    public sealed class WorkerProfile
    {
        public WorkerProfile()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;
            Tier = WorkerCapabilityTier.Standard;
            Strengths = new List<string>();
            IsAvailable = true;
            RegisteredUtc = DateTime.UtcNow;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public WorkerCapabilityTier Tier { get; set; }
        public List<string> Strengths { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime RegisteredUtc { get; set; }
    }
}
