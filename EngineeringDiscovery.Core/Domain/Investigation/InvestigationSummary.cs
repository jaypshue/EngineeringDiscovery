using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EngineeringDiscovery.Core.Domain.Investigation
{
    public sealed class InvestigationSummary
    {
        public string RepositoryName { get; init; } = string.Empty;

        // Discovery
        public int ProjectCount { get; init; }
        public int NamespaceCount { get; init; }
        public int TypeCount { get; init; }
        public int MemberCount { get; init; }

        // Engineering knowledge
        public int TotalArtifacts { get; init; }
        public int LayerViolations { get; init; }
        public int CircularProjectReferences { get; init; }
        public int EmptyControllers { get; init; }
        public int LongMethods { get; init; }

        public static InvestigationSummary CreateFrom(Investigation investigation)
        {
            if (investigation is null) throw new ArgumentNullException(nameof(investigation));

            var repoName = string.Empty;
            try
            {
                if (!string.IsNullOrWhiteSpace(investigation.RepositoryPath))
                {
                    repoName = Path.GetFileName(investigation.RepositoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrWhiteSpace(repoName)) repoName = investigation.RepositoryPath;
                }
            }
            catch { repoName = investigation.RepositoryPath ?? string.Empty; }

            var obs = investigation.Observations ?? Array.Empty<EngineeringDiscovery.Core.Models.DiscoveryObservation>();

            var projectCount = obs.Where(o => o.Kind == EngineeringDiscovery.Core.Models.ObservationKind.Project)
                                   .Select(o => o.Project)
                                   .Where(s => !string.IsNullOrWhiteSpace(s))
                                   .Distinct(StringComparer.OrdinalIgnoreCase)
                                   .Count();

            var namespaceCount = obs.Where(o => o.Kind == EngineeringDiscovery.Core.Models.ObservationKind.Namespace)
                                     .Select(o => o.Namespace)
                                     .Where(s => !string.IsNullOrWhiteSpace(s))
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .Count();

            var typeCount = obs.Where(o => o.Kind == EngineeringDiscovery.Core.Models.ObservationKind.Type)
                                .Select(o => o.Type)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count();

            var memberCount = obs.Count(o => o.Kind == EngineeringDiscovery.Core.Models.ObservationKind.Member);

            var totalArtifacts = investigation.Artifacts?.Count ?? 0;
            var layerViolations = investigation.Artifacts?.Count(a => string.Equals(a.Title, "Presentation layer depends on Infrastructure", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var circular = investigation.Artifacts?.Count(a => string.Equals(a.Title, "Circular project reference detected", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var emptyControllers = investigation.Artifacts?.Count(a => string.Equals(a.Title, "Empty controller detected", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var longMethods = investigation.Artifacts?.Count(a => string.Equals(a.Title, "Long method detected", StringComparison.OrdinalIgnoreCase)) ?? 0;

            return new InvestigationSummary
            {
                RepositoryName = repoName ?? string.Empty,
                ProjectCount = projectCount,
                NamespaceCount = namespaceCount,
                TypeCount = typeCount,
                MemberCount = memberCount,
                TotalArtifacts = totalArtifacts,
                LayerViolations = layerViolations,
                CircularProjectReferences = circular,
                EmptyControllers = emptyControllers,
                LongMethods = longMethods
            };
        }
    }
}
