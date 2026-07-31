using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class CircularProjectReferenceRule : IEngineeringRule
    {
        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            if (adjacency == null) return results;

            // Use a simple DFS to detect cycles. We'll record canonical cycles as sorted strings to avoid duplicates.
            var seenCycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var start in adjacency.Keys)
            {
                var stack = new Stack<string>();
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                void Dfs(string node)
                {
                    if (stack.Contains(node))
                    {
                        // cycle detected: extract cycle nodes from stack
                        var cycle = stack.Reverse().TakeWhile(n => !string.Equals(n, node, StringComparison.OrdinalIgnoreCase)).Reverse().ToList();
                        cycle.Add(node);
                        var canonical = string.Join("->", cycle.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
                        if (!seenCycles.Contains(canonical))
                        {
                            seenCycles.Add(canonical);
                            var title = "Circular project reference detected";
                            var description = $"Cycle:\n{string.Join(" -> ", cycle)}";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, EngineeringDiscovery.Core.Models.ArtifactType.CircularProjectReference));
                        }
                        return;
                    }

                    if (visited.Contains(node)) return;
                    visited.Add(node);
                    stack.Push(node);
                    try
                    {
                        if (adjacency.TryGetValue(node, out var neighbors))
                        {
                            foreach (var nb in neighbors)
                            {
                                Dfs(nb);
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        if (stack.Count > 0) stack.Pop();
                    }
                }

                try { Dfs(start); } catch { }
            }

            return results;
        }
    }
}
