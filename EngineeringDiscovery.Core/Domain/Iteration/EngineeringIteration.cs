using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.Iteration
{
    public sealed class EngineeringIteration
    {
        public EngineeringIteration()
        {
            Id = Guid.NewGuid();
            Goal = string.Empty;
            Status = IterationStatus.InProgress;
            Steps = new List<EngineeringIterationStep>();
            CreatedUtc = DateTime.UtcNow;
        }

        public Guid Id { get; set; }
        public string Goal { get; set; }
        public IterationStatus Status { get; set; }
        public List<EngineeringIterationStep> Steps { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public enum IterationStatus
    {
        InProgress,
        Paused,
        Completed
    }
}
