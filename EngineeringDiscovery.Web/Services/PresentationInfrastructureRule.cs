using System;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class PresentationInfrastructureRule : IEngineeringRule
    {
        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, string sourceLayer, string referencedLayer, string relationshipDescription)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if ((sourceLayer.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0 || sourceLayer.IndexOf("presentation", StringComparison.OrdinalIgnoreCase) >= 0)
                    && referencedLayer.IndexOf("infrastructure", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var title = "Presentation layer depends on Infrastructure";
                    var description = relationshipDescription;
                    results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description));
                }
            }
            catch { }
            return results;
        }
    }
}
