using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class DeepInheritanceRule : IEngineeringRule
    {
        private const int Threshold = 4;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (investigation?.Observations == null) return results;

                // Use type observations and simple name-based inference: look for 'inherits from' in descriptions.
                var inheritRelationships = investigation.Observations.Where(o => o.Kind == ObservationKind.Type && !string.IsNullOrWhiteSpace(o.Description) && o.Description.IndexOf("inherits from", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                // Build adjacency of type -> base type names inferred from description text
                var adjacencyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in inheritRelationships)
                {
                    try
                    {
                        // crude parse: "Type 'Child' inherits from 'Parent'"
                        var desc = t.Description;
                        var parts = desc.Split(new[] { "inherits from" }, StringSplitOptions.None);
                        if (parts.Length < 2) continue;
                        var left = parts[0];
                        var right = parts[1];
                        // extract type names in single quotes when present
                        string ExtractQuoted(string s)
                        {
                            var i1 = s.IndexOf('\'');
                            var i2 = s.IndexOf('\'', i1 + 1);
                            if (i1 >= 0 && i2 > i1) return s.Substring(i1 + 1, i2 - i1 - 1).Trim();
                            return s.Trim();
                        }
                        var child = ExtractQuoted(left);
                        var parent = ExtractQuoted(right);
                        if (!string.IsNullOrWhiteSpace(child) && !string.IsNullOrWhiteSpace(parent)) adjacencyMap[child] = parent;
                    }
                    catch { }
                }

                // Detect depths
                foreach (var kv in adjacencyMap)
                {
                    try
                    {
                        var depth = 0;
                        var current = kv.Key;
                        while (!string.IsNullOrWhiteSpace(current) && adjacencyMap.TryGetValue(current, out var parent))
                        {
                            depth++;
                            current = parent;
                            if (depth > Threshold)
                            {
                                var title = "Deep inheritance hierarchy";
                                var description = $"Type: {kv.Key}\n\nInheritance depth approximately {depth}.";
                                results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.DeepInheritance));
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return results;
        }
    }
}
