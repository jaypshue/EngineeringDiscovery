using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Tests.Tests;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class EngineeringPartnerChatIntegrationTests
    {
        private static (EngineeringPartner partner, WorkspaceState ws) CreatePartnerWithState()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var persistence = new InMemoryWorkspacePersistence();
            var ws = new WorkspaceState(persistence, new TestFingerprintService());
            var query = new EngineeringStateQuery(ws);
            var partner = new EngineeringPartner(repo, null, query);
            return (partner, ws);
        }

        [Fact]
        public async Task SendMessage_WorksWhenProjectStateIsNull()
        {
            var (partner, ws) = CreatePartnerWithState();
            // No ProjectState set - should work without error
            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id, "What is this project?");
            Assert.False(string.IsNullOrEmpty(reply));
        }

        [Fact]
        public async Task SendMessage_IncludesProjectIdentityInContext()
        {
            var (partner, ws) = CreatePartnerWithState();
            // Set up ProjectState with identity
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity
                {
                    Name = "InterviewReadyAI",
                    Description = "AI-powered interview preparation tool"
                }
            };

            var session = await partner.StartSessionAsync("Tell me about this project");
            var reply = await partner.SendMessageAsync(session.Id, "What are we building?");

            // The project state should be injected as a KnownFact visible in the model
            var model = await partner.GetWorkingMemoryAsync(session.Id);
            Assert.NotNull(model);
            var stateFact = model!.KnownFacts.Find(f => f.Key == "ProjectState");
            Assert.NotNull(stateFact);
            Assert.Contains("InterviewReadyAI", stateFact!.Value);
        }

        [Fact]
        public async Task SendMessage_IncludesLifecycleInContext()
        {
            var (partner, ws) = CreatePartnerWithState();
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity { Name = "MyProject" },
                Lifecycle = new ProjectLifecycle
                {
                    Phase = LifecyclePhase.ActiveDevelopment,
                    CurrentFocus = "Desktop overlay"
                }
            };

            var session = await partner.StartSessionAsync("Hi");
            await partner.SendMessageAsync(session.Id, "What are we working on?");

            var model = await partner.GetWorkingMemoryAsync(session.Id);
            var stateFact = model!.KnownFacts.Find(f => f.Key == "ProjectState");
            Assert.NotNull(stateFact);
            Assert.Contains("ActiveDevelopment", stateFact!.Value);
            Assert.Contains("Desktop overlay", stateFact.Value);
        }

        [Fact]
        public async Task SendMessage_IncludesResumePointInContext()
        {
            var (partner, ws) = CreatePartnerWithState();
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity { Name = "MyProject" },
                ResumePoint = new ResumePoint
                {
                    NextRecommendedAction = "Finish the desktop overlay animation"
                }
            };

            var session = await partner.StartSessionAsync("Hi");
            await partner.SendMessageAsync(session.Id, "Where should we resume?");

            var model = await partner.GetWorkingMemoryAsync(session.Id);
            var stateFact = model!.KnownFacts.Find(f => f.Key == "ProjectState");
            Assert.NotNull(stateFact);
            Assert.Contains("Finish the desktop overlay animation", stateFact!.Value);
        }

        [Fact]
        public async Task NewSession_CanAnswerStatusQuestion_WithoutPriorConversation()
        {
            var (partner, ws) = CreatePartnerWithState();
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity
                {
                    Name = "InterviewReadyAI",
                    Description = "AI interview preparation",
                    ProductVision = "Help candidates ace technical interviews"
                },
                Lifecycle = new ProjectLifecycle
                {
                    Phase = LifecyclePhase.Stabilization,
                    CurrentFocus = "Edge case handling"
                },
                ResumePoint = new ResumePoint
                {
                    Summary = "MVP+ complete, stabilizing edge cases",
                    NextRecommendedAction = "Fix remaining audio capture edge cases"
                }
            };

            // Brand new session - no prior conversation history
            var session = await partner.StartSessionAsync("What is the status?");
            var reply = await partner.SendMessageAsync(session.Id, "What is the current project status?");

            // The reply should reference project state (via fallback since no LLM configured)
            // At minimum, the KnownFacts should contain the ProjectState
            var model = await partner.GetWorkingMemoryAsync(session.Id);
            var stateFact = model!.KnownFacts.Find(f => f.Key == "ProjectState");
            Assert.NotNull(stateFact);
            Assert.Contains("InterviewReadyAI", stateFact!.Value);
            Assert.Contains("Stabilization", stateFact.Value);
        }

        [Fact]
        public async Task SendMessage_WorksWithoutStateQuery()
        {
            // EngineeringPartner with no IEngineeringStateQuery (backward compat)
            var repo = new InMemoryEngineeringModelRepository();
            var partner = new EngineeringPartner(repo, null, null);

            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id, "Test message");
            Assert.False(string.IsNullOrEmpty(reply));
        }

        [Fact]
        public async Task SendMessage_ProjectStateFactIsRefreshedOnEachMessage()
        {
            var (partner, ws) = CreatePartnerWithState();
            ws.ReplaceWorkspace(new Workspace());
            ws.ActiveWorkspace!.ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity { Name = "ProjectAlpha" }
            };

            var session = await partner.StartSessionAsync("Hi");
            await partner.SendMessageAsync(session.Id, "First message");

            // Change project identity between messages
            ws.ActiveWorkspace!.ProjectState.Identity!.Name = "ProjectBeta";

            await partner.SendMessageAsync(session.Id, "Second message");

            var model = await partner.GetWorkingMemoryAsync(session.Id);
            // Should have only one ProjectState fact (refreshed, not duplicated)
            var stateFacts = model!.KnownFacts.FindAll(f => f.Key == "ProjectState");
            Assert.Single(stateFacts);
            Assert.Contains("ProjectBeta", stateFacts[0].Value);
        }
    }
}
