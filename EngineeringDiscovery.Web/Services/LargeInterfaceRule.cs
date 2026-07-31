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
                if (investigation?.TypeObservations == null) return results;

                var interfaces = investigation.TypeObservations.Where(t => t.Kind == TypeKind.Interface);
                var grouped = interfaces.GroupBy(t => (Project: t.Project, Type: t.TypeName ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var type = g.First();
                        var memberCount = type.MemberCount;
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
