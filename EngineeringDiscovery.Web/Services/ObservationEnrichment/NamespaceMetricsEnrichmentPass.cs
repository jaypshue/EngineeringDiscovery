using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    // Placeholder pass: currently conservative. Validates the enrichment pipeline.
    internal class NamespaceMetricsEnrichmentPass : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                // Aggregate TypeObservations and MemberObservations into existing NamespaceObservation instances
                var nsList = investigation.NamespaceObservations;
                if (nsList == null || nsList.Count == 0) return;

                // Group TypeObservations by project+namespace
                var typesByNs = (investigation.TypeObservations ?? Array.Empty<EngineeringDiscovery.Core.Models.TypeObservation>())
                    .GroupBy(t => (t.Project ?? string.Empty, t.Namespace ?? string.Empty))
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Group MemberObservations by project+namespace
                var membersByNs = (investigation.MemberObservations ?? Array.Empty<EngineeringDiscovery.Core.Models.MemberObservation>())
                    .GroupBy(m => (m.Project ?? string.Empty, m.Namespace ?? string.Empty))
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var ns in nsList)
                {
                    try
                    {
                        var key = (Project: ns.Project ?? string.Empty, Namespace: ns.NamespaceName ?? string.Empty);
                        typesByNs.TryGetValue(key, out var typesInNs);
                        membersByNs.TryGetValue(key, out var membersInNs);

                        var types = typesInNs ?? new System.Collections.Generic.List<EngineeringDiscovery.Core.Models.TypeObservation>();
                        ns.TypeCount = types.Count;
                        ns.ClassCount = types.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Class);
                        ns.InterfaceCount = types.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Interface);
                        ns.RecordCount = types.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Record);
                        ns.StructCount = types.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Struct);
                        ns.EnumCount = types.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Enum);
                        ns.DelegateCount = types.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Delegate);
                        ns.PublicTypeCount = types.Count(t => string.Equals(t.Accessibility, "public", StringComparison.OrdinalIgnoreCase));
                        ns.InternalTypeCount = types.Count(t => string.Equals(t.Accessibility, "internal", StringComparison.OrdinalIgnoreCase));
                        ns.AbstractTypeCount = types.Count(t => t.IsAbstract);
                        ns.StaticTypeCount = types.Count(t => t.IsStatic);

                        var members = membersInNs ?? new System.Collections.Generic.List<EngineeringDiscovery.Core.Models.MemberObservation>();
                        ns.TypeCount = types.Count;

                        // If NamespaceObservation should track member totals, set via additional fields in the model.
                        // For now, do not modify the model beyond the requested properties.
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
