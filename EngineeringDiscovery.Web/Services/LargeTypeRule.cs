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
                if (investigation?.Observations == null) return results;

                // Prefer structured MemberObservations for accurate member counts
                if (investigation.MemberObservations == null) return results;
                var memberObs = investigation.MemberObservations.Where(m => !string.IsNullOrWhiteSpace(m.Type));
                var grouped = memberObs.GroupBy(m => (Project: m.Project, Type: m.Type ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var count = g.Count();
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
