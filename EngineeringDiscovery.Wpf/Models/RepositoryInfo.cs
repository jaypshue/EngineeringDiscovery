using System.Collections.Generic;

namespace EngineeringDiscovery.Wpf.Models
{
    // Simple projection of repository discovery into an engineering model
    public class RepositoryInfo
    {
        public string RepositoryRoot { get; set; } = string.Empty;

        // Friendly solution names discovered (file names)
        public IList<string> SolutionNames { get; } = new List<string>();

        // Project file paths discovered
        public IList<string> ProjectPaths { get; } = new List<string>();

        public int SolutionCount => SolutionNames.Count;
        public int ProjectCount => ProjectPaths.Count;
    }
}
