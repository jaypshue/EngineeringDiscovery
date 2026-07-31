using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class AsyncNamingRule : IEngineeringRule
    {
        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (investigation?.MemberObservations == null) return results;

                foreach (var m in investigation.MemberObservations)
                {
                    try
                    {
                        if (m.IsAsync && !(m.MemberName?.EndsWith("Async", StringComparison.OrdinalIgnoreCase) ?? false))
                        {
                            var title = "Async naming convention";
                            var description = $"Project: {m.Project}\nType: {m.Type}\nMethod: {m.MemberName}\n\nAsync methods should end with \"Async\".";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.AsyncNamingConvention));
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
