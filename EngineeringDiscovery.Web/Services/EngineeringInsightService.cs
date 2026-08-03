using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    public sealed record EngineeringInsight(string Subject, string Observation, string Category);

    /// <summary>
    /// Deterministic insight generator that analyzes the current Workspace and relationship graph
    /// to produce informational observations about the working set.
    /// </summary>
    public sealed class EngineeringInsightService
    {
        public EngineeringInsightService()
        {
        }

        public IEnumerable<EngineeringInsight> GetInsights(Workspace? workspace)
        {
            var results = new List<EngineeringInsight>();
            if (workspace is null) return results;
            var inv = workspace.Investigation;
            if (inv is null) return results;
            var graph = inv.RelationshipGraph;
            if (graph is null) return results;

            var ctx = workspace.CurrentTask?.Brief?.Context;
            if (ctx is null) return results;

            // Build set of explicit type ids in the working set
            var workingTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in ctx.TypeIds) if (!string.IsNullOrWhiteSpace(t)) workingTypes.Add(t);

            // Also include types that belong to selected namespaces/projects
            var typeObs = inv.TypeObservations ?? Enumerable.Empty<TypeObservation>();
            if (ctx.ProjectIds.Any() || ctx.NamespaceIds.Any())
            {
                foreach (var to in typeObs)
                {
                    var qn = to.QualifiedName ?? (string.IsNullOrWhiteSpace(to.Namespace) ? to.TypeName : to.Namespace + "." + to.TypeName);
                    if (string.IsNullOrWhiteSpace(qn)) continue;
                    if (ctx.ProjectIds.Contains(to.Project) || ctx.NamespaceIds.Contains(to.Namespace)) workingTypes.Add(qn);
                }
            }

            if (!workingTypes.Any()) return results;

            // Precompute project membership for working types
            var workingTypeProjects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var to in typeObs)
            {
                var qn = to.QualifiedName ?? (string.IsNullOrWhiteSpace(to.Namespace) ? to.TypeName : to.Namespace + "." + to.TypeName);
                if (string.IsNullOrWhiteSpace(qn)) continue;
                if (workingTypes.Contains(qn) && !string.IsNullOrWhiteSpace(to.Project)) workingTypeProjects[qn] = to.Project;
            }

            // Insight: workspace crosses project boundaries
            var distinctProjects = workingTypeProjects.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (distinctProjects.Length > 1)
            {
                results.Add(new EngineeringInsight("Workspace", $"Working set spans {distinctProjects.Length} projects", "CrossProject"));
            }

            // For each working type compute incoming/outgoing counts and relationships
            foreach (var wt in workingTypes)
            {
                var incoming = graph.GetIncomingRelationships(wt).Select(r => r.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var outgoing = graph.GetOutgoingRelationships(wt).Select(r => r.Target).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

                if (incoming.Length >= 5)
                {
                    results.Add(new EngineeringInsight(wt, $"High fan-in: {incoming.Length} incoming references", "FanIn"));
                }

                if (outgoing.Length >= 5)
                {
                    results.Add(new EngineeringInsight(wt, $"High fan-out: {outgoing.Length} outgoing references", "FanOut"));
                }

                // Implements multiple interfaces: count implementation outgoing edges
                var implOutgoing = graph.GetOutgoingRelationships(wt).Where(r => r.Type == RelationshipType.Implementation).Select(r => r.Target).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (implOutgoing.Length > 1)
                {
                    results.Add(new EngineeringInsight(wt, $"Implements multiple interfaces ({implOutgoing.Length})", "Implementation"));
                }

                // Participates in inheritance
                var parents = graph.GetParents(wt).ToArray();
                var children = graph.GetChildren(wt).ToArray();
                if (parents.Any() || children.Any())
                {
                    var parts = new List<string>();
                    if (parents.Any()) parts.Add($"parents: {parents.Count()}");
                    if (children.Any()) parts.Add($"children: {children.Count()}");
                    results.Add(new EngineeringInsight(wt, $"Inheritance participation ({string.Join(", ", parts)})", "Inheritance"));
                }
            }

            // Types referenced by multiple working set items: find targets referenced by >1 distinct working type
            var seedList = workingTypes.ToList();
            var targetCounts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var seed in seedList)
            {
                foreach (var (type, target) in graph.GetOutgoingRelationships(seed))
                {
                    if (string.IsNullOrWhiteSpace(target)) continue;
                    if (!targetCounts.TryGetValue(target, out var set)) { set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); targetCounts[target] = set; }
                    set.Add(seed);
                }
                foreach (var (type, source) in graph.GetIncomingRelationships(seed))
                {
                    if (string.IsNullOrWhiteSpace(source)) continue;
                    if (!targetCounts.TryGetValue(source, out var set)) { set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); targetCounts[source] = set; }
                    set.Add(seed);
                }
            }

            foreach (var kv in targetCounts)
            {
                if (kv.Value.Count > 1 && workingTypes.Contains(kv.Key) == false)
                {
                    results.Add(new EngineeringInsight(kv.Key, $"Referenced by {kv.Value.Count} items in the working set", "SharedReference"));
                }
            }

            return results;
        }
    }
}
