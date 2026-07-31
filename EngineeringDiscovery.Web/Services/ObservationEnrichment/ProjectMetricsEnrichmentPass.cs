using System;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal class ProjectMetricsEnrichmentPass : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                // Aggregate observations by project
                var projects = investigation.Observations.Select(o => o.Project).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                foreach (var proj in projects)
                {
                    try
                    {
                        var nsCount = investigation.TypeObservations?.Count(t => string.Equals(t.Project, proj, StringComparison.OrdinalIgnoreCase)) > 0 ? investigation.TypeObservations.Where(t => string.Equals(t.Project, proj, StringComparison.OrdinalIgnoreCase)).Select(t => t.Namespace).Distinct(StringComparer.OrdinalIgnoreCase).Count() : 0;
                        var typeObs = investigation.TypeObservations?.Where(t => string.Equals(t.Project, proj, StringComparison.OrdinalIgnoreCase)).ToList() ?? new System.Collections.Generic.List<EngineeringDiscovery.Core.Models.TypeObservation>();

                        var projectObs = new EngineeringDiscovery.Core.Models.ProjectObservation
                        {
                            Project = proj,
                            NamespaceCount = nsCount,
                            TypeCount = typeObs.Count,
                            ClassCount = typeObs.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Class),
                            InterfaceCount = typeObs.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Interface),
                            RecordCount = typeObs.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Record),
                            StructCount = typeObs.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Struct),
                            EnumCount = typeObs.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Enum),
                            DelegateCount = typeObs.Count(t => t.Kind == EngineeringDiscovery.Core.Models.TypeKind.Delegate),
                            MemberCount = typeObs.Sum(t => t.MemberCount)
                        };

                        try { investigation.SetProjectObservation(projectObs); } catch { }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
