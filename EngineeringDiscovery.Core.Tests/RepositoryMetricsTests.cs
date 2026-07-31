using System;
using System.Collections.Generic;
using System.Linq;

using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;
// RepositoryMetricsEnricher lives in the Web project; add a minimal bridge via internal helper
using EngineeringDiscovery.Web.Services.ObservationEnrichment;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class RepositoryMetricsTests
    {
        [Fact]
        public void ComputesMetricsForSimpleInheritance()
        {
            var inv = Investigation.Create(Guid.NewGuid(), "repo");
            inv.Start();

            var tBase = new TypeObservation { Project = "P", Namespace = "N", TypeName = "Base", QualifiedName = "P:N.Base", IsRootType = true };
            var tChild = new TypeObservation { Project = "P", Namespace = "N", TypeName = "Child", QualifiedName = "P:N.Child", BaseType = "P:N.Base" };

            inv.AddTypeObservation(tBase);
            inv.AddTypeObservation(tChild);

            var graph = new RepositoryRelationshipGraph();
            graph.AddInheritance("P:N.Child", "P:N.Base");
            inv.SetRelationshipGraph(graph);

            var enricher = new RepositoryMetricsEnricher();
            enricher.Enrich(inv);

            var metrics = inv.RepositoryMetrics;
            Assert.NotNull(metrics);
            Assert.Equal(1, metrics.RootTypeCount);
            Assert.Equal(1, metrics.LeafTypeCount);
            Assert.Equal(2, metrics.TotalTypes);
            Assert.True(metrics.PerTypeMetrics.ContainsKey("P:N.Base"));
            Assert.True(metrics.PerTypeMetrics.ContainsKey("P:N.Child"));
            Assert.Equal(1, metrics.PerTypeMetrics["P:N.Base"].DerivedTypeCount);
            Assert.Equal(0, metrics.PerTypeMetrics["P:N.Child"].DerivedTypeCount);
        }
    }
}
