using System;
using System.Linq;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal class TypeRelationshipEnricher : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                var types = (investigation.TypeObservations ?? Array.Empty<EngineeringDiscovery.Core.Models.TypeObservation>()).ToList();
                if (types.Count == 0) return;

                // TypeRelationshipEnricher no longer builds relationship collections. Relationship population
                // is centralized in GraphPopulationEnricher. Preserve lightweight heuristics here.
                foreach (var t in types)
                {
                    try
                    {
                        // IsFrameworkType / IsApplicationType: conservative heuristic using namespace patterns
                        var ns = t.Namespace ?? string.Empty;
                        if (ns.StartsWith("System", StringComparison.OrdinalIgnoreCase) || ns.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
                        {
                            t.IsFrameworkType = true;
                            t.IsApplicationType = false;
                        }
                        else
                        {
                            t.IsApplicationType = true;
                            t.IsFrameworkType = false;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
