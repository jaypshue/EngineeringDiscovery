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

                // Build a display -> QualifiedName lookup to resolve BaseType (display) to repository QualifiedName.
                var displayToQualified = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var typeByQualified = new Dictionary<string, EngineeringDiscovery.Core.Models.TypeObservation>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        var qn = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(qn)) continue;
                        typeByQualified[qn] = t;

                        if (!string.IsNullOrWhiteSpace(t.TypeName))
                        {
                            if (!displayToQualified.TryGetValue(t.TypeName!, out var list)) { list = new List<string>(); displayToQualified[t.TypeName!] = list; }
                            if (!list.Contains(qn, StringComparer.OrdinalIgnoreCase)) list.Add(qn);
                        }

                        if (!string.IsNullOrWhiteSpace(t.Namespace))
                        {
                            var nsKey = $"{t.Namespace}.{t.TypeName}";
                            if (!displayToQualified.TryGetValue(nsKey, out var list2)) { list2 = new List<string>(); displayToQualified[nsKey] = list2; }
                            if (!list2.Contains(qn, StringComparer.OrdinalIgnoreCase)) list2.Add(qn);
                        }
                    }
                    catch { }
                }

                // Build map: parentQualified -> list of derived TypeObservation
                var derivedMap = new Dictionary<string, List<EngineeringDiscovery.Core.Models.TypeObservation>>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(t.BaseType)) continue;

                        // Attempt to resolve BaseType display to a unique QualifiedName within the investigation
                        string? parentQualified = null;
                        if (typeByQualified.ContainsKey(t.BaseType!)) parentQualified = t.BaseType!;
                        else if (displayToQualified.TryGetValue(t.BaseType!, out var candidates))
                        {
                            var distinct = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                            if (distinct.Count == 1) parentQualified = distinct[0];
                        }

                        if (string.IsNullOrWhiteSpace(parentQualified)) continue;

                        if (!derivedMap.TryGetValue(parentQualified, out var list)) { list = new List<EngineeringDiscovery.Core.Models.TypeObservation>(); derivedMap[parentQualified] = list; }
                        list.Add(t);
                    }
                    catch { }
                }

                // Populate DerivedTypeCount and IsRootType/IsLeafType
                // Use QualifiedName as the repository-unique key when available.
                // Fall back to TypeName only if QualifiedName is missing to preserve behavior
                // during a transitional period.
                // Build dictionary keyed by QualifiedName (fallback to TypeName) but tolerate duplicate keys
                // by grouping and taking the first observation for any colliding keys. This avoids
                // ArgumentException when discovery produced duplicate canonical keys during transition.
                var typeByQualifiedName = types
                    .GroupBy(t => (t.QualifiedName ?? t.TypeName ?? string.Empty), StringComparer.OrdinalIgnoreCase)
                    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        // Use TypeName for display logic but use QualifiedName for any
                        // uniqueness-sensitive lookups.
                        var name = t.TypeName ?? string.Empty;
                        var key = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        derivedMap.TryGetValue(key, out var derived);
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
