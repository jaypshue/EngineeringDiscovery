using System;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class PresentationInfrastructureRule : IEngineeringRule
    {
        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (string.IsNullOrWhiteSpace(sourceLayer) || string.IsNullOrWhiteSpace(referencedLayer)) return results;

                if ((sourceLayer.IndexOf("web", StringComparison.OrdinalIgnoreCase) >= 0 || sourceLayer.IndexOf("presentation", StringComparison.OrdinalIgnoreCase) >= 0)
                    && referencedLayer.IndexOf("infrastructure", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var title = "Presentation layer depends on Infrastructure";
                    var description = relationshipDescription ?? $"{sourceLayer} depends on {referencedLayer}.";
                    results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, EngineeringDiscovery.Core.Models.ArtifactType.LayerViolation));
                }
            }
            catch { }
            return results;
        }
    }
}
