using System;
using System.IO;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class InvestigationContextRetrievalTests
    {
        [Fact]
        public void TranscriptFlowQuestion_RetrievesRelevantTypes()
        {
            // Use the real InterviewAssistant investigation if available
            var repoRoot = @"C:\projects\InterviewAssistant";
            if (!Directory.Exists(repoRoot)) return; // Skip on machines without the repo

            var engine = new EngineeringDiscovery.Web.Services.InvestigationEngine();
            var inv = engine.CreateInvestigation(repoRoot, null);
            Assert.NotNull(inv);

            var question = "Trace how a transcript message flows from the API to AI analysis.";
            var context = InvestigationContextRetrieval.RetrieveContextForQuestion(question, inv);

            Assert.False(string.IsNullOrWhiteSpace(context));

            // Should contain key types involved in the transcript flow
            Assert.Contains("TranscriptController", context);
            Assert.Contains("TranscriptService", context);
            Assert.Contains("InterviewOrchestrator", context);

            // Should contain member information
            Assert.Contains("ProcessTranscript", context);
            Assert.Contains("AddMessage", context);

            // Should contain dependency information
            Assert.Contains("Dependency", context);
            Assert.Contains("IAIService", context);

            // Should contain the data source note
            Assert.Contains("Static structural analysis", context);
        }

        [Fact]
        public void GenericQuestion_FallsBackToTypeSample()
        {
            var inv = CreateMinimalInvestigation();
            var context = InvestigationContextRetrieval.RetrieveContextForQuestion("What is this project about?", inv);

            Assert.False(string.IsNullOrWhiteSpace(context));
            Assert.Contains("ENGINEERING MODEL", context);
        }

        [Fact]
        public void NullInvestigation_ReturnsEmpty()
        {
            var context = InvestigationContextRetrieval.RetrieveContextForQuestion("test", null);
            Assert.Equal(string.Empty, context);
        }

        [Fact]
        public void EmptyQuestion_ReturnsEmpty()
        {
            var inv = CreateMinimalInvestigation();
            var context = InvestigationContextRetrieval.RetrieveContextForQuestion("", inv);
            Assert.Equal(string.Empty, context);
        }

        [Fact]
        public void KeywordMatching_FindsTypeByName()
        {
            var inv = CreateMinimalInvestigation();
            var context = InvestigationContextRetrieval.RetrieveContextForQuestion("Tell me about UserService", inv);

            Assert.Contains("UserService", context);
            Assert.Contains("GetUser", context); // member
        }

        private Investigation CreateMinimalInvestigation()
        {
            var inv = Investigation.Create(Guid.NewGuid(), @"C:\test\repo", "test", "owner", "TestProject");
            inv.Start();
            inv.AddTypeObservation(new TypeObservation
            {
                Project = "TestProject",
                Namespace = "TestProject.Services",
                TypeName = "UserService",
                QualifiedName = "TestProject:TestProject.Services.UserService",
                Kind = TypeKind.Class,
                MethodCount = 3,
                MemberCount = 5
            });
            inv.AddMemberObservation(new MemberObservation
            {
                Project = "TestProject",
                Namespace = "TestProject.Services",
                Type = "UserService",
                MemberName = "GetUser",
                Visibility = Visibility.Public,
                IsAsync = true,
                ParameterCount = 1,
                ReturnType = "Task<User>"
            });
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "TestProject contains UserService."));
            return inv;
        }
    }
}
