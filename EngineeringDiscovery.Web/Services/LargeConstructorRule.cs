using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class LargeConstructorRule : IEngineeringRule
    {
        private const int Threshold = 5;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (investigation?.MemberObservations == null) return results;

                var ctors = investigation.MemberObservations.Where(m => string.Equals(m.MemberName, m.Type, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(m.MemberName) == false && m.MemberName == ".ctor");
                foreach (var c in ctors)
                {
                    try
                    {
                        if (c.ParameterCount > Threshold)
                        {
                            var title = "Large constructor detected";
                            var description = $"Project: {c.Project}\nType: {c.Type}\n\nThe constructor declares {c.ParameterCount} parameters.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.LargeConstructor));
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
