using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    /// <summary>
    /// Computes deterministic, objective repository-wide metrics from normalized observations
    /// and the repository relationship graph. No engineering judgment or recommendations.
    /// </summary>
    public class RepositoryMetricsEnricher : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                var types = investigation.TypeObservations ?? Array.Empty<TypeObservation>();
                var graph = investigation.RelationshipGraph;

                // Compute basic repository metrics
                var totalProjects = investigation.Observations.Select(o => o.Project).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var totalNamespaces = types.Select(t => t.Namespace).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                var totalTypes = types.Count();
                var totalRelationships = graph != null ? graph.DerivedMap.Sum(kv => kv.Value.Count) : 0;

                // Per-type computed metrics derived from the RepositoryRelationshipGraph.
                // Do NOT read mutable dependency counts from TypeObservation; compute graph-derived metrics only from the graph.
                var perTypeMetrics = new Dictionary<string, EngineeringDiscovery.Core.Models.TypeMetrics>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        var qn = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(qn)) continue;

                        // Default metrics
                        var metrics = new EngineeringDiscovery.Core.Models.TypeMetrics
                        {
                            QualifiedName = qn,
                            // FanIn/FanOut/DirectDependency counts are dependency-graph derived. If no dependency graph
                            // exists on RepositoryRelationshipGraph, keep them as 0 to avoid reading mutable observation state.
                            FanIn = 0,
                            FanOut = 0,
                            DirectDependencyCount = 0,
                            DirectDependentCount = 0,
                            DerivedTypeCount = 0,
                            IsRoot = true,
                            IsLeaf = true
                        };

                        // If a relationship graph is available, derive metrics from it exclusively.
                        if (graph != null)
                        {
                            metrics.DerivedTypeCount = graph.DerivedMap.TryGetValue(qn, out var dset) ? dset.Count : 0;
                            metrics.IsRoot = !graph.TryGetParent(qn, out _);
                            metrics.IsLeaf = !(graph.DerivedMap.TryGetValue(qn, out var children) && children.Count > 0);
                            // Compute dependency metrics from the canonical graph.
                            var deps = graph.GetDependencies(qn).ToArray();
                            var dents = graph.GetDependents(qn).ToArray();
                            metrics.FanOut = deps.Length;
                            metrics.FanIn = dents.Length;
                            metrics.DirectDependencyCount = deps.Length;
                            metrics.DirectDependentCount = dents.Length;
                        }

                        // Inheritance depth: walk parents until none (use graph.ParentMap)
                        if (graph != null)
                        {
                            var depth = 0;
                            var current = qn;
                            while (graph.TryGetParent(current, out var parent))
                            {
                                depth++;
                                current = parent;
                                if (depth > 1000) break; // defensive
                            }
                            metrics.InheritanceDepth = depth;
                        }

                        perTypeMetrics[qn] = metrics;
                    }
                    catch { }
                }

                // Graph-level metrics
                var rootTypes = perTypeMetrics.Values.Count(m => m.IsRoot);
                var leafTypes = perTypeMetrics.Values.Count(m => m.IsLeaf);
                var isolatedTypes = perTypeMetrics.Values.Count(m => m.FanIn == 0 && m.FanOut == 0 && !m.IsRoot && !m.IsLeaf && m.DerivedTypeCount == 0);

                var repoMetrics = new EngineeringDiscovery.Core.Models.RepositoryMetrics
                {
                    TotalProjects = totalProjects,
                    TotalNamespaces = totalNamespaces,
                    TotalTypes = totalTypes,
                    TotalRelationships = totalRelationships,
                    RootTypeCount = rootTypes,
                    LeafTypeCount = leafTypes,
                    IsolatedTypeCount = isolatedTypes,
                    PerTypeMetrics = perTypeMetrics
                };

                investigation.SetRepositoryMetrics(repoMetrics);
            }
            catch { }
        }
    }
}
