using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Models
{
    public sealed class RepositoryMetrics
    {
        public int TotalProjects { get; set; }
        public int TotalNamespaces { get; set; }
        public int TotalTypes { get; set; }
        public int TotalRelationships { get; set; }
        public int RootTypeCount { get; set; }
        public int LeafTypeCount { get; set; }
        public int IsolatedTypeCount { get; set; }
        public Dictionary<string, TypeMetrics> PerTypeMetrics { get; set; } = new();
    }

    public sealed class TypeMetrics
    {
        public string QualifiedName { get; set; } = string.Empty;
        public int FanIn { get; set; }
        public int FanOut { get; set; }
        public int DirectDependencyCount { get; set; }
        public int DirectDependentCount { get; set; }
        public int InheritanceDepth { get; set; }
        public int DerivedTypeCount { get; set; }
        public bool IsRoot { get; set; }
        public bool IsLeaf { get; set; }
    }
}
