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
            _ = typePublicMethods;
            _ = sourceLayer;
            _ = referencedLayer;
            _ = relationshipDescription;
            try
            {
                // Use MemberObservations from the Investigation to determine controller emptiness
                if (investigation?.MemberObservations == null) return results;

                var grouped = investigation.MemberObservations.GroupBy(m => (Project: m.Project, Type: m.Type ?? string.Empty));
                var candidates = grouped.Select(g => new { g.Key.Project, Type = g.Key.Type, PublicCount = g.Count(m => m.Visibility == Visibility.Public) });

                foreach (var c in candidates)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(c.Type)) continue;
                        if (!c.Type.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)) continue;
                        if (c.PublicCount == 0)
                        {
                            var title = "Empty controller detected";
                            var description = $"Project: {c.Project}\nController: {c.Type}\n\nNo public endpoints were discovered.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, EngineeringDiscovery.Core.Models.ArtifactType.EmptyController));
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
