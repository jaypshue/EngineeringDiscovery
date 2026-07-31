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
                if (investigation?.Observations == null) return results;

                if (investigation.MemberObservations == null) return results;
                var members = investigation.MemberObservations;
                var grouped = members.GroupBy(o => (Project: o.Project, Type: o.Type ?? string.Empty));
                foreach (var g in grouped)
                {
                    try
                    {
                        var publicMethods = g.Count(o => o.Visibility == EngineeringDiscovery.Core.Models.Visibility.Public);
                        var privateHelpers = g.Count(o => o.Visibility == EngineeringDiscovery.Core.Models.Visibility.Private);
                        var totalMembers = g.Count();

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
