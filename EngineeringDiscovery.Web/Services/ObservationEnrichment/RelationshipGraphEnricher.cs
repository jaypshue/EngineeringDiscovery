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
            if (investigation == null) return;

            try
            {
                var types = investigation.TypeObservations ?? System.Array.Empty<TypeObservation>();
                var graph = new RepositoryRelationshipGraph();

                // Build lookup: display string -> list of QualifiedName candidates
                var displayToQualified = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(System.StringComparer.OrdinalIgnoreCase);
                var qualifiedSet = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        var qn = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(qn)) continue;
                        qualifiedSet.Add(qn);

                        if (!string.IsNullOrWhiteSpace(t.TypeName))
                        {
                            if (!displayToQualified.TryGetValue(t.TypeName!, out var l)) { l = new System.Collections.Generic.List<string>(); displayToQualified[t.TypeName!] = l; }
                            if (!l.Contains(qn, System.StringComparer.OrdinalIgnoreCase)) l.Add(qn);
                        }

                        if (!string.IsNullOrWhiteSpace(t.Namespace))
                        {
                            var nsKey = $"{t.Namespace}.{t.TypeName}";
                            if (!displayToQualified.TryGetValue(nsKey, out var l2)) { l2 = new System.Collections.Generic.List<string>(); displayToQualified[nsKey] = l2; }
                            if (!l2.Contains(qn, System.StringComparer.OrdinalIgnoreCase)) l2.Add(qn);
                        }
                    }
                    catch { }
                }

                foreach (var t in types)
                {
                    try
                    {
                        var childQualified = t.QualifiedName;
                        var parentDisplay = t.BaseType;
                        if (string.IsNullOrWhiteSpace(childQualified) || string.IsNullOrWhiteSpace(parentDisplay)) continue;

                        // Resolve parentDisplay to a unique QualifiedName within the investigation
                        string? parentQualified = null;
                        if (qualifiedSet.Contains(parentDisplay)) parentQualified = parentDisplay;
                        else if (displayToQualified.TryGetValue(parentDisplay!, out var candidates))
                        {
                            var distinct = System.Linq.Enumerable.Distinct(candidates, System.StringComparer.OrdinalIgnoreCase).ToList();
                            if (distinct.Count == 1) parentQualified = distinct[0];
                        }

                        if (string.IsNullOrWhiteSpace(parentQualified)) continue; // unresolved or ambiguous

                        graph.AddInheritance(childQualified, parentQualified);
                    }
                    catch { }
                }

                investigation.SetRelationshipGraph(graph);
            }
            catch { }
        }
    }
}
