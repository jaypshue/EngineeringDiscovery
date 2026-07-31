using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class EmptyControllerRule : IEngineeringRule
    {
        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? typePublicMethods = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (typePublicMethods == null) return results;

                // Keys are in format "{project}||{typeName}". Detect types ending with Controller and no public methods.
                foreach (var kv in typePublicMethods)
                {
                    try
                    {
                        var key = kv.Key ?? string.Empty;
                        var parts = key.Split(new[] {"||"}, StringSplitOptions.None);
                        if (parts.Length < 2) continue;
                        var proj = parts[0];
                        var typeName = parts[1];

                        if (string.IsNullOrWhiteSpace(typeName)) continue;
                        if (!typeName.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)) continue;

                        var methods = kv.Value ?? new List<string>();
                        if (methods.Count == 0)
                        {
                            var title = "Empty controller detected";
                            var description = $"{typeName} does not expose any public endpoints.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description));
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
