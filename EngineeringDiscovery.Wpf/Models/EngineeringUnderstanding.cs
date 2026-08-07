using System.Collections.Generic;

namespace EngineeringDiscovery.Wpf.Models
{
    // Lightweight projection of engineering understanding derived from repository observations
    public class EngineeringUnderstanding
    {
        // Repository/indexing
        public bool IsRepositoryIndexed { get; set; }

        // Basic counts
        public int SolutionCount { get; set; }
        public int ProjectCount { get; set; }

        // Simple detected platforms (WPF, ASP.NET Core, etc.)
        public IList<string> DetectedPlatforms { get; } = new List<string>();

        // High-level flags
        public bool IsMultiProjectSolution { get; set; }
    }
}
