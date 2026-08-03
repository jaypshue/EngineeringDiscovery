using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Workspace;

namespace EngineeringDiscovery.Web.Services
{
    public sealed record AdvisorEvidence(string Title, string Detail);
    public sealed record AdvisorResponse(IEnumerable<AdvisorEvidence> Evidence, string Interpretation);

    /// <summary>
    /// Lightweight deterministic advisor that reasons over the canonical engineering model
    /// (workspace, investigation, relationship graph, recommendations, insights) and
    /// produces evidence-backed responses to engineering questions.
    /// </summary>
    public sealed class EngineeringAdvisorService
    {
        public EngineeringAdvisorService()
        {
        }

        public AdvisorResponse Ask(string question, Workspace? workspace)
        {
            if (workspace is null) return new AdvisorResponse(Array.Empty<AdvisorEvidence>(), "No workspace available to answer the question.");

            var evidences = new List<AdvisorEvidence>();

            var active = workspace.CurrentTask;
            var ctx = active?.Brief?.Context;

            string interpretation = string.Empty;

            var q = (question ?? string.Empty).Trim();
            var ql = q.ToLowerInvariant();

            // Short-circuit: summarize current task / working set
            if (ql.Contains("explain") || ql.Contains("summarize") || ql.Contains("what should i understand") || ql.Contains("summarize my current task") )
            {
                if (active is null)
                {
                    interpretation = "No active task. Begin a Current Task to receive targeted advice.";
                    return new AdvisorResponse(evidences, interpretation);
                }

                interpretation = $"Summary for task '{active.Title}': {active.Description}. Goal: {active.Goal}.";
                evidences.Add(new AdvisorEvidence("Current Task", $"Title: {active.Title}; Goal: {active.Goal}"));
                evidences.Add(new AdvisorEvidence("Engineering Brief", active.Brief?.Objective ?? "(no objective)"));

                // Working set
                if (ctx is not null)
                {
                    evidences.Add(new AdvisorEvidence("Working Set Projects", string.Join(", ", ctx.ProjectIds.Any() ? ctx.ProjectIds : new[] { "(none)" } )));
                    evidences.Add(new AdvisorEvidence("Working Set Namespaces", string.Join(", ", ctx.NamespaceIds.Any() ? ctx.NamespaceIds : new[] { "(none)" } )));
                    evidences.Add(new AdvisorEvidence("Working Set Types", string.Join(", ", ctx.TypeIds.Any() ? ctx.TypeIds : new[] { "(none)" } )));
                }

                // Recommendations and insights
                var recs = new EngineeringRecommendationService().RecommendTypes(workspace).ToArray();
                if (recs.Any()) evidences.Add(new AdvisorEvidence("Recommendations", string.Join(", ", recs.Take(10))));

                var insights = new EngineeringInsightService().GetInsights(workspace).ToArray();
                if (insights.Any()) evidences.Add(new AdvisorEvidence("Insights", string.Join("; ", insights.Select(i => i.Observation).Take(10))));

                return new AdvisorResponse(evidences, interpretation);
            }

            // If question asks why a type was recommended
            if (ql.Contains("why") && ql.Contains("recommend"))
            {
                // Try to find a type name in the question by matching any recommended types
                var recs = new EngineeringRecommendationService().RecommendTypes(workspace).ToArray();
                var matched = recs.FirstOrDefault(r => q.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0);
                if (matched is null)
                {
                    interpretation = "I could not identify which recommended type you're asking about. Mention the fully-qualified type name.";
                    return new AdvisorResponse(evidences, interpretation);
                }

                evidences.Add(new AdvisorEvidence("Recommended Type", matched));
                // Provide relationship-based evidence
                var graph = workspace.Investigation?.RelationshipGraph;
                if (graph is not null)
                {
                    var incoming = graph.GetIncomingRelationships(matched).Select(r => r.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    var outgoing = graph.GetOutgoingRelationships(matched).Select(r => r.Target).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    evidences.Add(new AdvisorEvidence("Related (incoming)", incoming.Length > 0 ? string.Join(", ", incoming.Take(10)) : "(none)"));
                    evidences.Add(new AdvisorEvidence("Related (outgoing)", outgoing.Length > 0 ? string.Join(", ", outgoing.Take(10)) : "(none)"));
                    interpretation = $"Type '{matched}' was recommended because it has direct relations to items in your working set. See related incoming/outgoing relationships.";
                }
                else
                {
                    interpretation = "No relationship graph available to explain the recommendation.";
                }

                return new AdvisorResponse(evidences, interpretation);
            }

            // Default fallback: provide context + top insights
            interpretation = "I could not identify a specific intent from the question. Here are relevant context items and top insights.";
            if (active is not null)
            {
                evidences.Add(new AdvisorEvidence("Current Task", active.Title));
            }
            if (ctx is not null)
            {
                evidences.Add(new AdvisorEvidence("Working Set Types", string.Join(", ", ctx.TypeIds.Take(10))));
            }

            var topInsights = new EngineeringInsightService().GetInsights(workspace).Take(5).ToArray();
            if (topInsights.Any()) evidences.Add(new AdvisorEvidence("Top Insights", string.Join("; ", topInsights.Select(i => i.Observation))));

            return new AdvisorResponse(evidences, interpretation);
        }
    }
}
