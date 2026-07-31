using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class LargePublicSurfaceAreaRule : IEngineeringRule
    {
        private const int Threshold = 25;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (investigation?.MemberObservations == null) return results;

                var grouped = investigation.MemberObservations.GroupBy(m => (m.Project, Type: m.Type ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var publicCount = g.Count(m => m.Visibility == Visibility.Public);
                        if (publicCount > Threshold)
                        {
                            var title = "Large public surface area";
                            var description = $"Project: {g.Key.Project}\nType: {g.Key.Type}\n\nThe type exposes {publicCount} public members.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.LargePublicSurfaceArea));
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
