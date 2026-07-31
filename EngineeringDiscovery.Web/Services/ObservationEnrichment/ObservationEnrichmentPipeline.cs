using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal static class ObservationEnrichmentPipeline
    {
        public static IEnumerable<IObservationEnrichmentPass> Passes()
        {
            // Normalization must run first to ensure deterministic inputs for subsequent enrichers
            yield return new ObservationNormalizationPass();

            // Deterministic, static registration for now
            yield return new NamespaceMetricsEnrichmentPass();
            yield return new ProjectMetricsEnrichmentPass();
            // Build the canonical repository relationship graph (inheritance + dependencies)
            yield return new GraphPopulationEnricher();

            // Dependency enrichment now reads the canonical graph (no longer builds its own collections)
            yield return new TypeDependencyEnricher();
            yield return new RepositoryMetricsEnricher();
        }
    }
}
