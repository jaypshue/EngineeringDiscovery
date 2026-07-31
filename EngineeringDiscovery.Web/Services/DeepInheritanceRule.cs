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
                // Use structured TypeObservations where available. If base-type info is not present, remain conservative.
                if (investigation?.TypeObservations == null) return results;

                // Build adjacency from TypeObservation.BaseType when available
                var adjacencyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in investigation.TypeObservations)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(t.TypeName) || string.IsNullOrWhiteSpace(t.BaseType)) continue;
                        adjacencyMap[t.TypeName] = t.BaseType!;
                    }
                    catch { }
                }

                // Detect depths using only structured base-type links
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
