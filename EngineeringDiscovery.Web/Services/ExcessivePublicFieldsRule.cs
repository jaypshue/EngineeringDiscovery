using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class ExcessivePublicFieldsRule : IEngineeringRule
    {
        private const int Threshold = 5;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (investigation?.TypeObservations == null) return results;

                // Prefer structured TypeObservations field counts when available
                var grouped = investigation.TypeObservations.GroupBy(t => (Project: t.Project, Type: t.TypeName ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var type = g.First();
                        var fieldCount = type.FieldCount;
                        if (fieldCount > Threshold)
                        {
                            var title = "Excessive public fields";
                            var description = $"Project: {g.Key.Project}\nType: {g.Key.Type}\n\nThe type declares {fieldCount} public fields.";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.ExcessivePublicFields));
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
