using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    /// <summary>
    /// Durable identity and purpose of the project. Answers "what is this project"
    /// and "what is it trying to accomplish."
    /// </summary>
    public sealed class ProjectIdentity
    {
        public ProjectIdentity()
        {
            Name = string.Empty;
            Description = string.Empty;
            ProductVision = string.Empty;
            Repositories = new List<string>();
            EstablishedUtc = DateTime.UtcNow;
            LastUpdatedUtc = EstablishedUtc;
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public string ProductVision { get; set; }
        public List<string> Repositories { get; set; }
        public DateTime EstablishedUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }
}
