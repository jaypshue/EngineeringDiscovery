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
                var memberObservations = (investigation.MemberObservations ?? Array.Empty<MemberObservation>()).ToList();
                var graph = new RepositoryRelationshipGraph();

                if (types.Count == 0)
                {
                    investigation.SetRelationshipGraph(graph);
                    return;
                }

                // ED-181/ED-183: Discovery produces canonical TypeReference objects. No downstream
                // identity resolution should be performed here. GraphPopulationEnricher consumes
                // those canonical references directly.

                // Inheritance edges
                foreach (var t in types.OrderBy(x => x.QualifiedName ?? x.TypeName ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var childQualified = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        // Prefer canonical BaseTypeReference when provided by Discovery
                        if (!string.IsNullOrWhiteSpace(childQualified))
                        {
                            if (t.BaseTypeReference != null)
                            {
                                var parentQualified = t.BaseTypeReference.QualifiedName;
                                if (!string.IsNullOrWhiteSpace(parentQualified) && !string.Equals(childQualified, parentQualified, StringComparison.OrdinalIgnoreCase))
                                {
                                    graph.AddInheritance(childQualified, parentQualified);
                                }
                                else if (!t.BaseTypeReference.IsExternal && string.IsNullOrWhiteSpace(parentQualified))
                                {
                                    // Discovery signaled unresolved but non-external? Treat as external for now
                                    graph.IncrementExternalDependencyDiscardCount();
                                }
                            }
                            else if (!string.IsNullOrWhiteSpace(t.BaseType))
                            {
                                // Legacy display-only BaseType encountered; Discovery should provide BaseTypeReference.
                                // Treat display-only base types as external/unresolved.
                                graph.IncrementExternalDependencyDiscardCount();
                            }
                        }
                    }
                    catch { }
                }

                void ResolveAndAddRelationship(string from, TypeReference? tr, RelationshipType relationshipType)
                {
                    if (string.IsNullOrWhiteSpace(from) || tr == null) return;

                    // Use canonical QualifiedName produced by Discovery. If missing, treat as external/unresolved.
                    if (!string.IsNullOrWhiteSpace(tr.QualifiedName))
                    {
                        if (!string.Equals(from, tr.QualifiedName, StringComparison.OrdinalIgnoreCase))
                        {
                            graph.AddRelationship(from, tr.QualifiedName, relationshipType);
                        }
                    }
                    else
                    {
                        // Discovery signaled external or unresolved reference; count as discarded external dependency.
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
                            foreach (var ifaceRef in t.ImplementedInterfaces
                                .Where(x => x != null)
                                .DistinctBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, ifaceRef, RelationshipType.Implementation);
                            }
                        }

                        if (t.ConstructorParameterTypes != null)
                        {
                            foreach (var paramRef in t.ConstructorParameterTypes
                                .Where(x => x != null)
                                .DistinctBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, paramRef, RelationshipType.Dependency);
                            }
                        }

                        if (t.MethodParameterTypes != null)
                        {
                            foreach (var paramRef in t.MethodParameterTypes
                                .Where(x => x != null)
                                .DistinctBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, paramRef, RelationshipType.Dependency);
                            }
                        }

                        if (t.FieldTypes != null)
                        {
                            foreach (var fRef in t.FieldTypes
                                .Where(x => x != null)
                                .DistinctBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, fRef, RelationshipType.Dependency);
                            }
                        }

                        if (t.PropertyTypes != null)
                        {
                            foreach (var pRef in t.PropertyTypes
                                .Where(x => x != null)
                                .DistinctBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, pRef, RelationshipType.Dependency);
                            }
                        }

                        if (t.EventTypes != null)
                        {
                            foreach (var eRef in t.EventTypes
                                .Where(x => x != null)
                                .DistinctBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, eRef, RelationshipType.Dependency);
                            }
                        }

                        if (t.GenericArgumentTypes != null)
                        {
                            foreach (var gRef in t.GenericArgumentTypes
                                .Where(x => x != null)
                                .DistinctBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                                .OrderBy(x => (x.QualifiedName ?? x.DisplayName) ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                            {
                                ResolveAndAddRelationship(from, gRef, RelationshipType.Dependency);
                            }
                        }

                        var membersOfType = memberObservations.Where(m =>
                            string.Equals(m.Type ?? string.Empty, t.TypeName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                        foreach (var m in membersOfType)
                        {
                            try
                            {
                                // MemberObservation now contains canonical ReturnTypeReference and ParameterTypeReferences populated by Discovery.
                                if (m.ReturnTypeReference != null)
                                {
                                    ResolveAndAddRelationship(from, m.ReturnTypeReference, RelationshipType.Dependency);
                                }

                                if (m.ParameterTypeReferences != null)
                                {
                                    foreach (var pRef in m.ParameterTypeReferences.Where(x => x != null))
                                    {
                                        ResolveAndAddRelationship(from, pRef, RelationshipType.Dependency);
                                    }
                                }
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

        // Resolution maps removed: Discovery is the owner of canonical TypeReference identities.

        // TryResolveToQualified removed: Discovery is the owner of canonical TypeReference identities.
    }
}
