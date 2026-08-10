using System;
using System.Linq;
using Xunit;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Core.Tests
{
    public class TypeRelationshipEnricherTests
    {
        [Fact]
        public void Enricher_AllowsDuplicateTypeNamesDifferentNamespaces()
        {
            var t1 = new TypeObservation { Project = "Core", Namespace = "EngineeringDiscovery.Core.Models", TypeName = "InterviewQuestionAnalysis", QualifiedName = "Core:EngineeringDiscovery.Core.Models.InterviewQuestionAnalysis" };
            var t2 = new TypeObservation { Project = "Web", Namespace = "EngineeringDiscovery.Web.Models", TypeName = "InterviewQuestionAnalysis", QualifiedName = "Web:EngineeringDiscovery.Web.Models.InterviewQuestionAnalysis" };

            var inv = Investigation.Create(Guid.NewGuid(), "repo");
            inv.Start();
            inv.AddTypeObservation(t1);
            inv.AddTypeObservation(t2);

            // TypeRelationshipEnricher is internal to the Web project; use the pipeline registration
            // to obtain one of the enrichment passes. ObservationEnrichmentPipeline is internal too,
            // so instantiate the Web enricher via its fully-qualified internal type using reflection.
            var enricherType = typeof(object).Assembly.GetType("EngineeringDiscovery.Web.Services.ObservationEnrichment.TypeRelationshipEnricher");
            if (enricherType == null)
            {
                // Fallback: directly reference the type if available at compile time
                enricherType = typeof(EngineeringDiscovery.Web.Services.ObservationEnrichment.TypeRelationshipEnricher);
            }
            var enricher = Activator.CreateInstance(enricherType) as dynamic;

            // Should not throw due to duplicate simple TypeName
            enricher.Enrich(inv);

            // Both observations should still be present and have IsLeafType set (no derived types)
            Assert.Equal(0, t1.DerivedTypeCount);
            Assert.Equal(0, t2.DerivedTypeCount);
            Assert.True(t1.IsLeafType);
            Assert.True(t2.IsLeafType);
        }
    }
}
