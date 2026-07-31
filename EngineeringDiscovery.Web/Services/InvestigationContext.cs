using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EngineeringDiscovery.Web.Services
{
    internal class InvestigationContext
    {
        public InvestigationContext(string? solutionPath)
        {
            SolutionPath = solutionPath;
            Diagnostics = new List<string>();
            DiscoveredProjects = new List<(string Name, string Path)>();
            Exclusions = new[] { "\\bin\\", "\\obj\\", "/bin/", "/obj/", ".git", ".vs", "node_modules" };
        }

        public string? SolutionPath { get; private set; }

        public string? SolutionDirectory => !string.IsNullOrWhiteSpace(SolutionPath) ? Path.GetDirectoryName(SolutionPath) : null;

        public List<(string Name, string Path)> DiscoveredProjects { get; }

        // Structured collection of member observations populated during MemberDiscoveryStep
        public List<EngineeringDiscovery.Core.Models.MemberObservation> MemberObservations { get; } = new();

        // Structured collection of type observations populated during TypeDiscoveryStep
        public List<EngineeringDiscovery.Core.Models.TypeObservation> TypeObservations { get; } = new();

        public List<string> Diagnostics { get; }

        public string[] Exclusions { get; }

        public void AddDiagnostic(string message)
        {
            try { Diagnostics.Add(message); } catch { }
        }

        public bool IsExcludedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var lower = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar).ToLowerInvariant();
            foreach (var ex in Exclusions)
            {
                if (lower.Contains(ex.ToLowerInvariant())) return true;
            }
            return false;
        }

        // If the provided SolutionPath points to a temporary file, attempt to locate an original solution with the same filename
        // by searching parent directories starting from the current working directory. This preserves repository context when possible.
        public void EnsureOriginalSolutionContext()
        {
            // This method is intentionally removed for ED-901. The Discovery Engine now operates on repository roots
            // and should not attempt to reconcile temporary solution copies back to repository context.
        }
    }
}
