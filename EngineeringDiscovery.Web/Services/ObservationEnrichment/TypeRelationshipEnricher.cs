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

                // Build map: typeName -> list of derived types
                var derivedMap = new Dictionary<string, List<EngineeringDiscovery.Core.Models.TypeObservation>>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(t.BaseType)) continue;
                        if (!derivedMap.TryGetValue(t.BaseType!, out var list)) { list = new List<EngineeringDiscovery.Core.Models.TypeObservation>(); derivedMap[t.BaseType!] = list; }
                        list.Add(t);
                    }
                    catch { }
                }

                // Populate DerivedTypeCount and IsRootType/IsLeafType
                var typeByName = types.ToDictionary(t => t.TypeName ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        var name = t.TypeName ?? string.Empty;
                        derivedMap.TryGetValue(name, out var derived);
                        t.DerivedTypeCount = derived?.Count ?? 0;
                        t.IsRootType = (t.BaseType == null || t.BaseType == string.Empty) && (t.DerivedTypeCount > 0);
                        t.IsLeafType = (t.DerivedTypeCount == 0);

                        // ImplementsInterfaceCount: count of implemented interfaces recorded in discovery via BaseType? Not available reliably here.
                        // If discovery had recorded ImplementedInterfaceCount, keep it; otherwise leave default (0).

                        // IsFrameworkType / IsApplicationType: conservative heuristic using project name or namespace patterns
                        try
                        {
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
                    catch { }
                }
            }
            catch { }
        }
    }
}
