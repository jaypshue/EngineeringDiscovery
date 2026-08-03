using System;

namespace EngineeringDiscovery.Core.Domain.CurrentTask
{
    public sealed class EngineeringBrief
    {
        public EngineeringBrief()
        {
            Objective = string.Empty;
            Notes = string.Empty;
            ImplementationThoughts = string.Empty;
            LastUpdatedUtc = DateTime.UtcNow;
            Context = new EngineeringContext();
        }

        public string Objective { get; set; }

        public string Notes { get; set; }

        public string ImplementationThoughts { get; set; }

        public DateTime LastUpdatedUtc { get; private set; }

        public void Touch()
        {
            LastUpdatedUtc = DateTime.UtcNow;
        }

        public EngineeringContext Context { get; set; }
    }
}
