using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class LongMethodRule : IEngineeringRule
    {
        private const int ThresholdLines = 50;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? methodLineCounts = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (methodLineCounts == null) return results;

                foreach (var kv in methodLineCounts)
                {
                    try
                    {
                        var key = kv.Key ?? string.Empty; // format: Project||Type||Method
                        var parts = key.Split(new[] {"||"}, StringSplitOptions.None);
                        if (parts.Length < 3) continue;
                        var proj = parts[0];
                        var typeName = parts[1];
                        var methodName = parts[2];
                        var v = kv.Value?.FirstOrDefault();
                        if (!int.TryParse(v, out var lines)) continue;
                        if (lines <= ThresholdLines) continue;

                        var title = "Long method detected";
                        var description = $"Project: {proj}\nType: {typeName}\nMethod: {methodName}\n\nThe method contains approximately {lines} source lines.";
                        results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.LongMethod));
                    }
                    catch { }
                }
            }
            catch { }
            return results;
        }
    }
}
