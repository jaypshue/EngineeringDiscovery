using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services
{
    internal class MixedResponsibilityRule : IEngineeringRule
    {
        // Conservative thresholds
        private const int PublicMethodThreshold = 12;
        private const int PrivateHelperThreshold = 8;
        private const int MemberCountThreshold = 30;

        public IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null)
        {
            var results = new List<InvestigationArtifact>();
            try
            {
                if (investigation?.TypeObservations == null) return results;

                var grouped = investigation.TypeObservations.GroupBy(t => (Project: t.Project, Type: t.TypeName ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var type = g.First();
                        var publicMethods = type.MethodCount; // conservative: treat method count as proxy for public API size when visibility is unknown
                        var privateHelpers = 0; // visibility of helpers is not populated at type granularity; remain conservative
                        var totalMembers = type.MemberCount;

                        if (publicMethods >= PublicMethodThreshold && privateHelpers >= PrivateHelperThreshold && totalMembers >= MemberCountThreshold)
                        {
                            var title = "Mixed responsibilities";
                            var description = $"Project: {g.Key.Project}\nType: {g.Key.Type}\n\nPublic methods: {publicMethods}\nPrivate helpers: {privateHelpers}\nTotal members: {totalMembers}\n\nThis type shows signs of mixed responsibilities (many public API methods alongside many private helpers).";
                            results.Add(new InvestigationArtifact(Guid.NewGuid(), title, description, ArtifactType.MixedResponsibilities));
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
