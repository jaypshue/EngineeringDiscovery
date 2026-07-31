using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class LargeInterfaceRule : IEngineeringRule
    {
        private const int Threshold = 20;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (investigation?.Observations == null) return results;

                // Interface types identified by ObservationKind.Type where description contains 'interface'
                var typeObs = investigation.Observations.Where(o => o.Kind == EngineeringDiscovery.Core.Models.ObservationKind.Type && (o.Description?.IndexOf("interface", StringComparison.OrdinalIgnoreCase) >= 0));
                var grouped = typeObs.GroupBy(o => (o.Project, Type: o.Type ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var memberCount = investigation.Observations.Count(o => o.Kind == EngineeringDiscovery.Core.Models.ObservationKind.Member && string.Equals(o.Type, g.Key.Type, StringComparison.OrdinalIgnoreCase) && string.Equals(o.Project, g.Key.Project, StringComparison.OrdinalIgnoreCase));
                        if (memberCount > Threshold)
                        {
                            var title = "Large interface detected";
                            var description = $"Project: {g.Key.Project}\nInterface: {g.Key.Type}\n\nThe interface declares {memberCount} members.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.LargeInterface));
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
