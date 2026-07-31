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
            yield return new TypeRelationshipEnricher();
        }
    }
}
