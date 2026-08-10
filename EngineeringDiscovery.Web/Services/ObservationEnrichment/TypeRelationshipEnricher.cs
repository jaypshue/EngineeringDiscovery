using System;
using System.Linq;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    public class TypeRelationshipEnricher : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                var types = (investigation.TypeObservations ?? Array.Empty<EngineeringDiscovery.Core.Models.TypeObservation>()).ToList();
                if (types.Count == 0) return;

                // Prefer a unique type identity: QualifiedName when available, otherwise compose a stable key
                // using Project + Namespace + TypeName. This avoids collisions when different types share the
                // same simple TypeName across projects or namespaces.
                static string GetIdentity(EngineeringDiscovery.Core.Models.TypeObservation t)
                    => t.QualifiedName ?? ($"{t.Project}:{t.Namespace}.{t.TypeName}" ?? string.Empty);

                // Build derived map by canonical identity (unique), preserving prior DerivedTypeCount behavior.
                var derivedMap = new Dictionary<string, List<EngineeringDiscovery.Core.Models.TypeObservation>>(StringComparer.OrdinalIgnoreCase);

                foreach (var t in types)
                {
                    try
                    {
                        var parentKey = t.BaseTypeReference?.QualifiedName ?? t.BaseType ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(parentKey)) continue;

                        // parentKey may be a display name; prefer matching against qualified identity where possible.
                        if (!derivedMap.TryGetValue(parentKey, out var list))
                        {
                            list = new List<EngineeringDiscovery.Core.Models.TypeObservation>();
                            derivedMap[parentKey] = list;
                        }
                        list.Add(t);
                    }
                    catch { }
                }

                // Populate DerivedTypeCount/IsRootType/IsLeafType using unique identities
                // Map types by identity to enable lookup without colliding on simple TypeName
                var typeByIdentity = types.ToDictionary(t => GetIdentity(t), StringComparer.OrdinalIgnoreCase);

                foreach (var t in types)
                {
                    try
                    {
                        var id = GetIdentity(t);
                        // derived map keys might be qualified names; attempt to resolve by identity
                        derivedMap.TryGetValue(t.QualifiedName ?? t.TypeName ?? string.Empty, out var derivedByName);
                        derivedMap.TryGetValue(id, out var derivedById);
                        var derived = derivedById ?? derivedByName;

                        t.DerivedTypeCount = derived?.Count ?? 0;
                        t.IsRootType = (string.IsNullOrWhiteSpace(t.BaseType) && (t.DerivedTypeCount > 0));
                        t.IsLeafType = (t.DerivedTypeCount == 0);

                        // IsFrameworkType / IsApplicationType heuristic using namespace patterns
                        var ns = t.Namespace ?? string.Empty;
                        if (ns.StartsWith("System", StringComparison.OrdinalIgnoreCase) || ns.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase))
                        {
                            t.IsFrameworkType = true;
                            t.IsApplicationType = false;
                        }
                        else
                        {
                            t.IsApplicationType = true;
                            t.IsFrameworkType = false;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
