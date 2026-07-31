using System;
using System.Linq;
using Xunit;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Core.Tests
{
    public class RepositoryRelationshipGraphTests
    {
        [Fact]
        public void AddInheritance_AllowsDistinctQualifiedNamesWithSameTypeName()
        {
            var graph = new RepositoryRelationshipGraph();

            // Two distinct types that share the same TypeName but have different QualifiedName
            var qn1 = "Core:EngineeringDiscovery.Core.Models.InterviewQuestionAnalysis";
            var qn2 = "Web:EngineeringDiscovery.Web.Models.InterviewQuestionAnalysis";

            var base1 = "Core:EngineeringDiscovery.Core.Models.BaseType";
            var base2 = "Web:EngineeringDiscovery.Web.Models.BaseType";

            graph.AddInheritance(qn1, base1);
            graph.AddInheritance(qn2, base2);

            Assert.Contains(qn1, graph.GetDerivedTypes(base1));
            Assert.Contains(qn2, graph.GetDerivedTypes(base2));
        }

        [Fact]
        public void DictionaryByQualifiedName_AllowsDuplicatesInTypeName()
        {
            var t1 = new TypeObservation { Project = "Core", Namespace = "EngineeringDiscovery.Core.Models", TypeName = "InterviewQuestionAnalysis", QualifiedName = "Core:EngineeringDiscovery.Core.Models.InterviewQuestionAnalysis" };
            var t2 = new TypeObservation { Project = "Web", Namespace = "EngineeringDiscovery.Web.Models", TypeName = "InterviewQuestionAnalysis", QualifiedName = "Web:EngineeringDiscovery.Web.Models.InterviewQuestionAnalysis" };

            var types = new[] { t1, t2 };
            var dict = types.ToDictionary(t => t.QualifiedName ?? t.TypeName ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            Assert.Equal(2, dict.Count);
            Assert.True(dict.ContainsKey(t1.QualifiedName));
            Assert.True(dict.ContainsKey(t2.QualifiedName));
        }

        [Fact]
        public void AddDependency_IsStoredAndDeduplicated()
        {
            var graph = new RepositoryRelationshipGraph();
            graph.AddRelationship("A:Ns.A", "B:Ns.B", RelationshipType.Dependency);
            graph.AddRelationship("A:Ns.A", "B:Ns.B", RelationshipType.Dependency); // duplicate

            var deps = graph.GetDependencies("A:Ns.A").ToList();
            Assert.Single(deps);
            Assert.Contains("B:Ns.B", deps);
        }
    }
}
