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
                if (investigation?.Observations == null) return results;

                // Public fields are represented as Member observations and MemberObservation entries capture visibility.
                if (investigation.MemberObservations == null) return results;
                var publicFields = investigation.MemberObservations.Where(m => m.Visibility == EngineeringDiscovery.Core.Models.Visibility.Public && !string.IsNullOrWhiteSpace(m.MemberName) /* field name */);
                var grouped = publicFields.GroupBy(o => (Project: o.Project, Type: o.Type ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var fieldCount = g.Count();
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
