using System;
using System.Linq;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal class TypeDependencyEnricher : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                var types = (investigation.TypeObservations ?? Array.Empty<EngineeringDiscovery.Core.Models.TypeObservation>()).ToList();
                if (types.Count == 0) return;

                // Build outgoing dependency map from discovery observations: for each type, which other types it references
                var outgoing = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                var incoming = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                // Discovery may have recorded direct references in TypeObservation.BaseType and possibly other fields.
                foreach (var t in types)
                {
                    try
                    {
                        var from = t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(from)) continue;

                        if (!outgoing.TryGetValue(from, out var set)) { set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); outgoing[from] = set; }

                        // Base type reference
                        if (!string.IsNullOrWhiteSpace(t.BaseType))
                        {
                            set.Add(t.BaseType!);
                            if (!incoming.TryGetValue(t.BaseType!, out var inSet)) { inSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase); incoming[t.BaseType!] = inSet; }
                            inSet.Add(from);
                        }

                        // If discovery recorded implemented interfaces as a comma-separated list in BaseType or another field, skip - avoid speculation.
                    }
                    catch { }
                }

                // Populate counts
                foreach (var t in types)
                {
                    try
                    {
                        var name = t.TypeName ?? string.Empty;
                        t.OutgoingDependencyCount = outgoing.TryGetValue(name, out var oset) ? oset.Count : 0;
                        t.IncomingDependencyCount = incoming.TryGetValue(name, out var iset) ? iset.Count : 0;
                        t.IsDependencyHub = t.IncomingDependencyCount > 10 || t.OutgoingDependencyCount > 10; // conservative
                        t.IsDependencyLeaf = t.OutgoingDependencyCount == 0;
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
