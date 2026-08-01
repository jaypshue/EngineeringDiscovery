using System;
using System.Linq;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

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

                // Discovery now produces canonical TypeReference collections on TypeObservation. Build outgoing/incoming
                // dependency maps by consuming those canonical references only.
                foreach (var t in types)
                {
                    try
                    {
                        var from = t.QualifiedName ?? t.TypeName ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(from)) continue;

                        if (!outgoing.TryGetValue(from, out var set)) { set = new HashSet<string>(StringComparer.OrdinalIgnoreCase); outgoing[from] = set; }

                        // BaseTypeReference
                        if (t.BaseTypeReference != null && !string.IsNullOrWhiteSpace(t.BaseTypeReference.QualifiedName))
                        {
                            var resolved = t.BaseTypeReference.QualifiedName;
                            set.Add(resolved);
                            if (!incoming.TryGetValue(resolved, out var inSet)) { inSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase); incoming[resolved] = inSet; }
                            inSet.Add(from);
                        }

                        // Implemented interfaces
                        if (t.ImplementedInterfaces != null)
                        {
                            foreach (var iface in t.ImplementedInterfaces.Where(x => x != null && !string.IsNullOrWhiteSpace(x.QualifiedName)))
                            {
                                try
                                {
                                    var resolved = iface.QualifiedName!;
                                    set.Add(resolved);
                                    if (!incoming.TryGetValue(resolved, out var inSet2)) { inSet2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase); incoming[resolved] = inSet2; }
                                    inSet2.Add(from);
                                }
                                catch { }
                            }
                        }

                        // Member-level references (constructor/method/field/property/event/generic args)
                        var refs = new List<TypeReference>();
                        if (t.ConstructorParameterTypes != null) refs.AddRange(t.ConstructorParameterTypes);
                        if (t.MethodParameterTypes != null) refs.AddRange(t.MethodParameterTypes);
                        if (t.FieldTypes != null) refs.AddRange(t.FieldTypes);
                        if (t.PropertyTypes != null) refs.AddRange(t.PropertyTypes);
                        if (t.EventTypes != null) refs.AddRange(t.EventTypes);
                        if (t.GenericArgumentTypes != null) refs.AddRange(t.GenericArgumentTypes);

                        foreach (var tr in refs.Where(x => x != null && !string.IsNullOrWhiteSpace(x.QualifiedName)))
                        {
                            try
                            {
                                var resolved = tr.QualifiedName!;
                                set.Add(resolved);
                                if (!incoming.TryGetValue(resolved, out var inSet3)) { inSet3 = new HashSet<string>(StringComparer.OrdinalIgnoreCase); incoming[resolved] = inSet3; }
                                inSet3.Add(from);
                            }
                            catch { }
                        }
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
                            var deps = graph.GetDependencies(qn).ToArray();
                            var dents = graph.GetDependents(qn).ToArray();
                            outgoingCount = deps.Length;
                            incomingCount = dents.Length;

                            // Also set the direct dependency/dependent counts for the richer metrics
                            // DirectDependencyCount == FanOut; DirectDependentCount == FanIn
                            // These are stored on RepositoryMetrics via RepositoryMetricsEnricher; here we only
                            // populate TypeObservation read-only counts for legacy UI if needed.
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
