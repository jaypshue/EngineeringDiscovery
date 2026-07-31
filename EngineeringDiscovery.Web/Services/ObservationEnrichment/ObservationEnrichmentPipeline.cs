using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal static class ObservationEnrichmentPipeline
    {
        public static IEnumerable<IObservationEnrichmentPass> Passes()
        {
            // Deterministic, static registration for now
            yield return new NamespaceMetricsEnrichmentPass();
            yield return new ProjectMetricsEnrichmentPass();
            // Build the inheritance relationship graph first
            yield return new TypeRelationshipEnricher();
            yield return new RelationshipGraphEnricher();

            // Dependency enrichment relies on relationship graph being available
            yield return new TypeDependencyEnricher();
        }
    }
}
