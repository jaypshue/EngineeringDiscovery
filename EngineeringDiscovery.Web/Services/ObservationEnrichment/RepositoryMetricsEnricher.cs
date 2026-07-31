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
    internal class RepositoryMetricsEnricher : IObservationEnrichmentPass
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

                // Per-type computed metrics: Fan-In (incoming deps), Fan-Out (outgoing deps), InheritanceDepth, DirectDependencyCount, DirectDependentCount
                // For dependency counts we conservatively use existing fields if available (IncomingDependencyCount/OutgoingDependencyCount) otherwise 0
                var perTypeMetrics = new Dictionary<string, EngineeringDiscovery.Core.Models.TypeMetrics>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        var qn = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(qn)) continue;

                        var metrics = new EngineeringDiscovery.Core.Models.TypeMetrics
                        {
                            QualifiedName = qn,
                            FanIn = t.IncomingDependencyCount,
                            FanOut = t.OutgoingDependencyCount,
                            DirectDependencyCount = t.OutgoingDependencyCount,
                            DirectDependentCount = t.IncomingDependencyCount,
                            DerivedTypeCount = t.DerivedTypeCount,
                            IsRoot = t.IsRootType,
                            IsLeaf = t.IsLeafType
                        };

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
