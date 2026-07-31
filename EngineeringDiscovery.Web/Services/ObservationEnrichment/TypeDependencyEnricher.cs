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

                // Build display->Qualified lookup for resolution
                var displayToQualified = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var qualifiedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var t in types)
                {
                    try
                    {
                        var qn = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(qn)) continue;
                        qualifiedSet.Add(qn);

                        if (!string.IsNullOrWhiteSpace(t.TypeName))
                        {
                            if (!displayToQualified.TryGetValue(t.TypeName!, out var list)) { list = new List<string>(); displayToQualified[t.TypeName!] = list; }
                            if (!list.Contains(qn, StringComparer.OrdinalIgnoreCase)) list.Add(qn);
                        }

                        if (!string.IsNullOrWhiteSpace(t.Namespace))
                        {
                            var nsKey = $"{t.Namespace}.{t.TypeName}";
                            if (!displayToQualified.TryGetValue(nsKey, out var list2)) { list2 = new List<string>(); displayToQualified[nsKey] = list2; }
                            if (!list2.Contains(qn, StringComparer.OrdinalIgnoreCase)) list2.Add(qn);
                        }
                    }
                    catch { }
                }

                // Discovery may have recorded direct references in TypeObservation.BaseType and possibly other fields.
                foreach (var t in types)
                {
                    try
                    {
                        var from = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(from)) continue;

                        if (!outgoing.TryGetValue(from, out var set)) { set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); outgoing[from] = set; }

                        // Base type reference: resolve BaseType display to QualifiedName (only within investigation)
                        if (!string.IsNullOrWhiteSpace(t.BaseType))
                        {
                            string? resolved = null;
                            if (qualifiedSet.Contains(t.BaseType!)) resolved = t.BaseType!;
                            else if (displayToQualified.TryGetValue(t.BaseType!, out var candidates))
                            {
                                var distinct = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                                if (distinct.Count == 1) resolved = distinct[0];
                            }

                            if (!string.IsNullOrWhiteSpace(resolved))
                            {
                                set.Add(resolved);
                                if (!incoming.TryGetValue(resolved, out var inSet)) { inSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase); incoming[resolved] = inSet; }
                                inSet.Add(from);
                            }
                        }

                        // If discovery recorded implemented interfaces as a comma-separated list in BaseType or another field, skip - avoid speculation.
                    }
                    catch { }
                }

                // Populate counts from the canonical graph if available; otherwise fall back to previously computed maps
                var graph = investigation.RelationshipGraph;
                foreach (var t in types)
                {
                    try
                    {
                        var qn = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(qn)) continue;

                        int outgoingCount = 0;
                        int incomingCount = 0;

                        if (graph != null)
                        {
                            // Use deterministic ordering by materializing into arrays
                            outgoingCount = graph.GetDependencies(qn).ToArray().Length;
                            incomingCount = graph.GetDependents(qn).ToArray().Length;
                        }
                        else
                        {
                            // fallback to the maps we built earlier (keyed by QualifiedName)
                            outgoingCount = outgoing.TryGetValue(qn, out var oset) ? oset.Count : 0;
                            incomingCount = incoming.TryGetValue(qn, out var iset) ? iset.Count : 0;
                        }

                        t.OutgoingDependencyCount = outgoingCount;
                        t.IncomingDependencyCount = incomingCount;
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
