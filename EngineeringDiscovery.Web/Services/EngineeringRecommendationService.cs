using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    /// <summary>
    /// Simple deterministic recommendation service that suggests directly-related types
    /// based on the repository relationship graph and the current EngineeringContext.
    /// </summary>
    public sealed class EngineeringRecommendationService
    {
        public EngineeringRecommendationService()
        {
        }

        public IEnumerable<string> RecommendTypes(Workspace? workspace)
        {
            if (workspace is null) return Enumerable.Empty<string>();
            var inv = workspace.Investigation;
            if (inv is null) return Enumerable.Empty<string>();
            var graph = inv.RelationshipGraph;
            if (graph is null) return Enumerable.Empty<string>();

            var ctx = workspace.CurrentTask?.Brief?.Context;

            // Build set of existing ids in working set so we never recommend them
            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ctx is not null)
            {
                foreach (var p in ctx.ProjectIds) existing.Add(p);
                foreach (var n in ctx.NamespaceIds) existing.Add(n);
                foreach (var t in ctx.TypeIds) existing.Add(t);
            }

            // Seed types to explore: explicit type ids in the context plus any types that belong
            // to selected projects or namespaces.
            var seeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ctx is not null)
            {
                foreach (var t in ctx.TypeIds) if (!string.IsNullOrWhiteSpace(t)) seeds.Add(t);

                var typeObs = inv.TypeObservations ?? Enumerable.Empty<TypeObservation>();
                foreach (var to in typeObs)
                {
                    var qn = to.QualifiedName ?? (string.IsNullOrWhiteSpace(to.Namespace) ? to.TypeName : to.Namespace + "." + to.TypeName);
                    if (string.IsNullOrWhiteSpace(qn)) continue;
                    if (ctx.ProjectIds.Any() && !string.IsNullOrWhiteSpace(to.Project) && ctx.ProjectIds.Contains(to.Project))
                        seeds.Add(qn);
                    if (ctx.NamespaceIds.Any() && !string.IsNullOrWhiteSpace(to.Namespace) && ctx.NamespaceIds.Contains(to.Namespace))
                        seeds.Add(qn);
                }
            }

            if (!seeds.Any()) return Enumerable.Empty<string>();

            var recommendations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in seeds)
            {
                // Outgoing relationships: related targets
                foreach (var (type, target) in graph.GetOutgoingRelationships(s))
                {
                    if (type == RelationshipType.Unknown) continue;
                    if (string.IsNullOrWhiteSpace(target)) continue;
                    if (existing.Contains(target)) continue;
                    recommendations.Add(target);
                }

                // Incoming relationships: related sources
                foreach (var (type, source) in graph.GetIncomingRelationships(s))
                {
                    if (type == RelationshipType.Unknown) continue;
                    if (string.IsNullOrWhiteSpace(source)) continue;
                    if (existing.Contains(source)) continue;
                    recommendations.Add(source);
                }
            }

            return recommendations.OrderBy(r => r).ToList();
        }
    }
}
