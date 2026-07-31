using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class ExcessiveParameterRule : IEngineeringRule
    {
        private const int Threshold = 5;

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
                        if (m.ParameterCount > Threshold)
                        {
                            var title = "Excessive parameter count";
                            var description = $"Project: {m.Project}\nType: {m.Type}\nMethod: {m.MemberName}\n\nThe method declares {m.ParameterCount} parameters.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.ExcessiveParameterCount));
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
