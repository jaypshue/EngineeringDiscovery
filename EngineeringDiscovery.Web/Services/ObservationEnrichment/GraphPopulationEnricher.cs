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

                // Build resolution maps used to translate discovery display names to repository QualifiedName.
                // TRANSITIONAL: this identity-resolution logic exists only because Discovery currently emits
                // display names for some references. Long-term goal: Discovery should emit QualifiedName for
                // all references and this helper can be removed.
                var displayToQualified = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var typeByQualified = new Dictionary<string, TypeObservation>(StringComparer.OrdinalIgnoreCase);
                var qualifiedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                BuildResolutionMaps(types, displayToQualified, qualifiedSet, typeByQualified, graph);

                // Inheritance edges
                foreach (var t in types)
                {
                    try
                    {
                        var childQualified = t.QualifiedName;
                        var parentDisplay = t.BaseType;
                        if (string.IsNullOrWhiteSpace(childQualified) || string.IsNullOrWhiteSpace(parentDisplay)) continue;

                        // Resolve parentDisplay to a unique QualifiedName within the investigation (transitional)
                        if (!TryResolveToQualified(parentDisplay, qualifiedSet, displayToQualified, out var parentQualified)) continue;

                        if (string.IsNullOrWhiteSpace(parentQualified)) continue; // unresolved or ambiguous

                        graph.AddInheritance(childQualified, parentQualified);
                    }
                    catch { }
                }

                // Dependency edges: derive from discovery artifacts (members) and TypeObservation fields.
                // Candidate sources: BaseType, Implemented interfaces, Member return/parameter/field/property types.
                // Only add dependency edges between repository types (qualified names present in this investigation).
                var memberObservations = investigation.MemberObservations ?? System.Array.Empty<EngineeringDiscovery.Core.Models.MemberObservation>();

                foreach (var t in types)
                {
                    try
                    {
                        var from = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(from)) continue;

                        // BaseType
                        if (!string.IsNullOrWhiteSpace(t.BaseType))
                        {
                            if (TryResolveToQualified(t.BaseType, qualifiedSet, displayToQualified, out var resolved))
                            {
                                if (!string.Equals(from, resolved, StringComparison.OrdinalIgnoreCase))
                                {
                                    graph.AddRelationship(from, resolved, RelationshipType.Dependency);
                                }
                            }
                            else
                            {
                                // Candidate resolved to an external/framework type: record for telemetry and skip
                                graph.IncrementExternalDependencyDiscardCount();
                            }
                        }

                        // Member-based dependencies (return types and parameter types are stored as strings in MemberObservation)
                        var membersOfType = memberObservations.Where(m => string.Equals(m.Type ?? string.Empty, t.TypeName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                        foreach (var m in membersOfType)
                        {
                            try
                            {
                                // Return type
                                if (!string.IsNullOrWhiteSpace(m.ReturnType))
                                {
                                    if (TryResolveToQualified(m.ReturnType, qualifiedSet, displayToQualified, out var target))
                                    {
                                        if (!string.Equals(from, target, StringComparison.OrdinalIgnoreCase))
                                        {
                                            graph.AddRelationship(from, target, RelationshipType.Dependency);
                                        }
                                    }
                                    else
                                    {
                                        graph.IncrementExternalDependencyDiscardCount();
                                    }
                                }

                                // Parameters: stored only as counts in MemberObservation; parameter types are not available here.
                                // Future work: extend MemberObservation to include parameter type names so we can add edges from parameters.
                            }
                            catch { }
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

        private static void BuildResolutionMaps(
            List<TypeObservation> types,
            Dictionary<string, List<string>> displayToQualified,
            HashSet<string> qualifiedSet,
            Dictionary<string, TypeObservation> typeByQualified,
            RepositoryRelationshipGraph graph)
        {
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
        }

        // Transitional helper: resolve a display name to a unique QualifiedName within the investigation when possible.
        // Once discovery emits canonical QualifiedName for all references this helper can be removed.
        private static bool TryResolveToQualified(string? display, HashSet<string> qualifiedSet, Dictionary<string, List<string>> displayToQualified, out string? qualified)
        {
            qualified = null;
            if (string.IsNullOrWhiteSpace(display)) return false;

            if (qualifiedSet.Contains(display))
            {
                qualified = display;
                return true;
            }

            if (displayToQualified.TryGetValue(display!, out var candidates))
            {
                var distinct = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (distinct.Count == 1)
                {
                    qualified = distinct[0];
                    return true;
                }
            }

            return false;
        }
    }
}
