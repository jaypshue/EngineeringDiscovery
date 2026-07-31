using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class LargeTypeRule : IEngineeringRule
    {
        private const int Threshold = 40;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                // Prefer structured TypeObservations for accurate member counts
                if (investigation.TypeObservations == null) return results;
                var grouped = investigation.TypeObservations.GroupBy(t => (Project: t.Project, Type: t.TypeName ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var type = g.First();
                        var count = type.MemberCount;
                        if (count > Threshold)
                        {
                            var title = "Large type detected";
                            var description = $"Project: {g.Key.Project}\nType: {g.Key.Type}\n\nThe type contains {count} members.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.LargeType));
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
