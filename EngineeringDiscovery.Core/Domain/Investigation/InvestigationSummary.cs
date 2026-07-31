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
        public int ExcessiveParameterCount { get; init; }
        public int LargeConstructors { get; init; }
        public int AsyncNamingIssues { get; init; }
        public int LargePublicSurfaceAreas { get; init; }
        public int LargeTypes { get; init; }
        public int LargeInterfaces { get; init; }
        public int DeepInheritanceHierarchies { get; init; }
        public int ExcessivePublicFields { get; init; }
        public int MixedResponsibilities { get; init; }

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
            var layerViolations = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.LayerViolation) ?? 0;
            var circular = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.CircularProjectReference) ?? 0;
            var emptyControllers = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.EmptyController) ?? 0;
            var longMethods = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.LongMethod) ?? 0;
            var excessiveParams = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.ExcessiveParameterCount) ?? 0;
            var largeCtors = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.LargeConstructor) ?? 0;
            var asyncNaming = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.AsyncNamingConvention) ?? 0;
            var largeSurface = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.LargePublicSurfaceArea) ?? 0;
            var largeTypes = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.LargeType) ?? 0;
            var largeIfaces = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.LargeInterface) ?? 0;
            var deepInheritance = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.DeepInheritance) ?? 0;
            var excessiveFields = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.ExcessivePublicFields) ?? 0;
            var mixedResp = investigation.Artifacts?.Count(a => a.Type == EngineeringDiscovery.Core.Models.ArtifactType.MixedResponsibilities) ?? 0;

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
                LongMethods = longMethods,
                ExcessiveParameterCount = excessiveParams,
                LargeConstructors = largeCtors,
                AsyncNamingIssues = asyncNaming,
                LargePublicSurfaceAreas = largeSurface
                ,LargeTypes = largeTypes
                ,LargeInterfaces = largeIfaces
                ,DeepInheritanceHierarchies = deepInheritance
                ,ExcessivePublicFields = excessiveFields
                ,MixedResponsibilities = mixedResp
            };
        }
    }
}
