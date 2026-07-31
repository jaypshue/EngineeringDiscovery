using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class DeepInheritanceRule : IEngineeringRule
    {
        // Use relationship metadata populated by enrichment passes
        private const int DerivedTypeThreshold = 10;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (investigation?.TypeObservations == null) return results;

                foreach (var t in investigation.TypeObservations)
                {
                    try
                    {
                        if (t.DerivedTypeCount > DerivedTypeThreshold)
                        {
                            var title = "Deep inheritance hierarchy";
                            var description = $"Project: {t.Project}\nType: {t.TypeName}\n\nDerived types: {t.DerivedTypeCount}.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.DeepInheritance));
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
