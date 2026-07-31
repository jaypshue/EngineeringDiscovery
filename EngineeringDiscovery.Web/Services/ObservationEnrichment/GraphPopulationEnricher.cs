using System;
using System.Linq;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    /// <summary>
    /// Single responsible component that populates the canonical RepositoryRelationshipGraph
    /// with inheritance and dependency edges derived from normalized TypeObservations.
    /// Also preserves prior behavior by populating DerivedTypeCount/IsRootType/IsLeafType
    /// on TypeObservation so existing downstream code continues to behave.
    /// </summary>
    internal class GraphPopulationEnricher : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                var types = (investigation.TypeObservations ?? Array.Empty<TypeObservation>()).ToList();
                var graph = new RepositoryRelationshipGraph();

                if (types.Count == 0)
                {
                    investigation.SetRelationshipGraph(graph);
                    return;
                }

                // Build lookup: display string -> list of QualifiedName candidates
                var displayToQualified = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var typeByQualified = new Dictionary<string, TypeObservation>(StringComparer.OrdinalIgnoreCase);
                var qualifiedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var t in types)
                {
                    try
                    {
                        var qn = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(qn)) continue;
                        qualifiedSet.Add(qn);
                        typeByQualified[qn] = t;
                        graph.AddNode(qn);

                        if (!string.IsNullOrWhiteSpace(t.TypeName))
                        {
                            if (!displayToQualified.TryGetValue(t.TypeName!, out var l)) { l = new List<string>(); displayToQualified[t.TypeName!] = l; }
                            if (!l.Contains(qn, StringComparer.OrdinalIgnoreCase)) l.Add(qn);
                        }

                        if (!string.IsNullOrWhiteSpace(t.Namespace))
                        {
                            var nsKey = $"{t.Namespace}.{t.TypeName}";
                            if (!displayToQualified.TryGetValue(nsKey, out var l2)) { l2 = new List<string>(); displayToQualified[nsKey] = l2; }
                            if (!l2.Contains(qn, StringComparer.OrdinalIgnoreCase)) l2.Add(qn);
                        }
                    }
                    catch { }
                }

                // Inheritance edges
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
                            var distinct = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                            if (distinct.Count == 1) parentQualified = distinct[0];
                        }

                        if (string.IsNullOrWhiteSpace(parentQualified)) continue; // unresolved or ambiguous

                        graph.AddInheritance(childQualified, parentQualified);
                    }
                    catch { }
                }

                // Dependency edges (conservative: use BaseType as a recorded reference if available)
                foreach (var t in types)
                {
                    try
                    {
                        var from = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(from)) continue;

                        if (!string.IsNullOrWhiteSpace(t.BaseType))
                        {
                            string? resolved = null;
                            if (qualifiedSet.Contains(t.BaseType!)) resolved = t.BaseType!;
                            else if (displayToQualified.TryGetValue(t.BaseType!, out var candidates))
                            {
                                var distinct = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                                if (distinct.Count == 1) resolved = distinct[0];
                            }

                            if (!string.IsNullOrWhiteSpace(resolved))
                            {
                                graph.AddRelationship(from, resolved, RelationshipType.Dependency);
                            }
                        }
                    }
                    catch { }
                }

                // Populate TypeObservation relationship-derived fields (DerivedTypeCount, IsRootType, IsLeafType)
                foreach (var t in types)
                {
                    try
                    {
                        var key = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(key)) continue;

                        t.DerivedTypeCount = graph.DerivedMap.TryGetValue(key, out var dset) ? dset.Count : 0;
                        t.IsRootType = !graph.TryGetParent(key, out _);
                        t.IsLeafType = !(graph.DerivedMap.TryGetValue(key, out var children) && children.Count > 0);
                    }
                    catch { }
                }

                investigation.SetRelationshipGraph(graph);
            }
            catch { }
        }
    }
}
