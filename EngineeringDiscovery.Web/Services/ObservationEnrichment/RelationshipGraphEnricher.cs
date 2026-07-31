using System;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal class RelationshipGraphEnricher : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            // RelationshipGraphEnricher previously built the canonical graph. Responsibility has
            // moved to GraphPopulationEnricher. This pass is now intentionally a no-op to ensure
            // there is a single authoritative graph builder in the pipeline.
            return;
        }
    }
}
