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
                var displayToQualified = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var typeByQualified = new Dictionary<string, TypeObservation>(StringComparer.OrdinalIgnoreCase);
                var qualifiedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                BuildResolutionMaps(types, displayToQualified, qualifiedSet, typeByQualified, graph);

                // Inheritance edges
                foreach (var t in types.OrderBy(x => x.QualifiedName ?? x.TypeName ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var childQualified = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        var parentDisplay = t.BaseType;
                        if (string.IsNullOrWhiteSpace(childQualified) || string.IsNullOrWhiteSpace(parentDisplay)) continue;

                        if (!TryResolveToQualified(parentDisplay, qualifiedSet, displayToQualified, out var parentQualified))
                        {
                            graph.IncrementExternalDependencyDiscardCount();
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(parentQualified)) continue; // unresolved or ambiguous
                        if (string.Equals(childQualified, parentQualified, StringComparison.OrdinalIgnoreCase)) continue;

                        graph.AddInheritance(childQualified, parentQualified);
                    }
                    catch { }
                }

                void ResolveAndAddRelationship(string from, string? display, RelationshipType relationshipType)
                {
                    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(display)) return;

                    if (TryResolveToQualified(display, qualifiedSet, displayToQualified, out var qualified))
                    {
                        if (!string.IsNullOrWhiteSpace(qualified) && !string.Equals(from, qualified, StringComparison.OrdinalIgnoreCase))
                        {
                            graph.AddRelationship(from, qualified, relationshipType);
                        }
                    }
                    else
                    {
                        graph.IncrementExternalDependencyDiscardCount();
                    }
                }

                foreach (var t in types.OrderBy(x => x.QualifiedName ?? x.TypeName ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var from = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(from)) continue;

                        if (t.ImplementedInterfaces != null)
                        {
                            foreach (var ifaceDisplay in t.ImplementedInterfaces
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, ifaceDisplay, RelationshipType.Implementation);
                            }
                        }

                        if (t.ConstructorParameterTypes != null)
                        {
                            foreach (var paramDisplay in t.ConstructorParameterTypes
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, paramDisplay, RelationshipType.Dependency);
                            }
                        }

                        if (t.MethodParameterTypes != null)
                        {
                            foreach (var paramDisplay in t.MethodParameterTypes
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, paramDisplay, RelationshipType.Dependency);
                            }
                        }

                        if (t.FieldTypes != null)
                        {
                            foreach (var fDisplay in t.FieldTypes
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, fDisplay, RelationshipType.Dependency);
                            }
                        }

                        if (t.PropertyTypes != null)
                        {
                            foreach (var pDisplay in t.PropertyTypes
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, pDisplay, RelationshipType.Dependency);
                            }
                        }

                        if (t.EventTypes != null)
                        {
                            foreach (var eDisplay in t.EventTypes
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, eDisplay, RelationshipType.Dependency);
                            }
                        }

                        if (t.GenericArgumentTypes != null)
                        {
                            foreach (var gDisplay in t.GenericArgumentTypes
                                .Where(x => !string.IsNullOrWhiteSpace(x))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, gDisplay, RelationshipType.Dependency);
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
