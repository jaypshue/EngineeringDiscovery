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
    public class ConversationalStateEstablishmentTests
    {
        private static (EngineeringPartner partner, WorkspaceState ws, ProjectStateService pss) CreatePartnerWithFullServices()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var persistence = new InMemoryWorkspacePersistence();
            var ws = new WorkspaceState(persistence, new TestFingerprintService());
            var query = new EngineeringStateQuery(ws);
            var pss = new ProjectStateService(ws);
            var partner = new EngineeringPartner(repo, null, query, pss);
            return (partner, ws, pss);
        }

        [Fact]
        public async Task ProjectDescription_WithNoState_CreatesProposal()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            var reply = await partner.SendMessageAsync(session.Id,
                "We're building EngineOS as an engineering workspace that coordinates coding agents like Copilot and Kiro.");

            // Should propose, not persist
            Assert.Contains("Should I record", reply);
            Assert.Null(ws.ActiveWorkspace?.ProjectState?.Identity);

            // Proposal should be staged in KnownFacts
            var model = await partner.GetWorkingMemoryAsync(session.Id);
            var proposal = model!.KnownFacts.Find(f => f.Key == "ProposedProjectIdentity");
            Assert.NotNull(proposal);
        }

        [Fact]
        public async Task ProposalNotPersistedBeforeConfirmation()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            await partner.SendMessageAsync(session.Id,
                "We're building InterviewReadyAI to help candidates prepare for technical interviews.");

            // ProjectState should NOT have Identity set
            // (ProjectState may not even exist yet since we haven't confirmed)
            var state = ws.ActiveWorkspace?.ProjectState;
            Assert.True(state == null || state.Identity == null);
        }

        [Fact]
        public async Task ConfirmationWithPendingProposal_PersistsIdentity()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            // First: describe the project (creates proposal)
            await partner.SendMessageAsync(session.Id,
                "We're building EngineOS as an engineering workspace that coordinates coding workers.");

            // Then: confirm
            var reply = await partner.SendMessageAsync(session.Id, "Yes");

            // ProjectState should now have Identity
            Assert.Contains("Recorded", reply);
            Assert.NotNull(ws.ActiveWorkspace?.ProjectState?.Identity);
            Assert.Contains("EngineOS", ws.ActiveWorkspace!.ProjectState!.Identity!.Name);
        }

        [Fact]
        public async Task ConfirmationConsumesProposal()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            await partner.SendMessageAsync(session.Id,
                "We're building TestProject as a developer tool.");
            await partner.SendMessageAsync(session.Id, "Yes, that's correct.");

            // The proposal KnownFact should be consumed
            var model = await partner.GetWorkingMemoryAsync(session.Id);
            var proposal = model!.KnownFacts.Find(f => f.Key == "ProposedProjectIdentity");
            Assert.Null(proposal);
        }

        [Fact]
        public async Task YesWithNoPendingProposal_DoesNotMutateState()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            // Say "yes" without any prior proposal
            var reply = await partner.SendMessageAsync(session.Id, "Yes");

            // Should NOT create ProjectState
            Assert.Null(ws.ActiveWorkspace?.ProjectState?.Identity);
            // Should not contain "Recorded" (no state mutation happened)
            Assert.DoesNotContain("Recorded", reply);
        }

        [Fact]
        public async Task LifecycleProposal_StagedWithoutMutation()
        {
            var (partner, ws, pss) = CreatePartnerWithFullServices();
            // Pre-establish identity so we have a ProjectState
            pss.SetProjectIdentity(new ProjectIdentity { Name = "TestProject" });

            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id,
                "We're in active development focused on the desktop workspace UI.");

            // Should propose lifecycle, not persist
            Assert.Contains("Should I record", reply);
            // Lifecycle should still be null or at the default phase (Inception) since we haven't confirmed
            var lifecycle = ws.ActiveWorkspace!.ProjectState!.Lifecycle;
            Assert.True(lifecycle == null || lifecycle.Phase == LifecyclePhase.Inception);
        }

        [Fact]
        public async Task LifecycleConfirmation_PersistsChange()
        {
            var (partner, ws, pss) = CreatePartnerWithFullServices();
            pss.SetProjectIdentity(new ProjectIdentity { Name = "TestProject" });

            var session = await partner.StartSessionAsync("Hello");
            await partner.SendMessageAsync(session.Id,
                "We're in active development focused on the desktop workspace UI.");
            var reply = await partner.SendMessageAsync(session.Id, "Yes");

            Assert.Contains("Recorded", reply);
            Assert.Equal(LifecyclePhase.ActiveDevelopment, ws.ActiveWorkspace!.ProjectState!.Lifecycle!.Phase);
            Assert.Contains("desktop workspace", ws.ActiveWorkspace.ProjectState.Lifecycle.CurrentFocus, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExistingIdentity_DoesNotTriggerRepeatedProposals()
        {
            var (partner, ws, pss) = CreatePartnerWithFullServices();
            // Pre-establish identity
            pss.SetProjectIdentity(new ProjectIdentity { Name = "EngineOS", Description = "Existing description" });

            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id,
                "We're building EngineOS to coordinate coding agents.");

            // Should NOT propose again since identity already exists
            Assert.DoesNotContain("Should I record this as the project identity", reply);
        }

        [Fact]
        public async Task DeterministicFallback_WorksWithoutLLM()
        {
            // No conversation service = deterministic path
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            // Describe project
            var reply1 = await partner.SendMessageAsync(session.Id,
                "We're building a testing framework for distributed systems.");
            Assert.Contains("Should I record", reply1);

            // Confirm
            var reply2 = await partner.SendMessageAsync(session.Id, "Yes");
            Assert.Contains("Recorded", reply2);

            // State should be persisted
            Assert.NotNull(ws.ActiveWorkspace?.ProjectState?.Identity);
        }

        // ─── Regression Tests: Confirmation Phrase Variants ─────────────────────────

        [Theory]
        [InlineData("Yes")]
        [InlineData("Yes, that's right.")]
        [InlineData("Correct.")]
        [InlineData("That's right. Record it.")]
        [InlineData("Record it.")]
        [InlineData("Yes, that's right. Record it.")]
        public async Task VariousConfirmationPhrases_ConfirmPendingProposal(string confirmation)
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            // Create a proposal
            await partner.SendMessageAsync(session.Id,
                "We're building EngineOS as an engineering workspace that coordinates coding workers.");

            // Confirm with various phrases
            var reply = await partner.SendMessageAsync(session.Id, confirmation);

            Assert.Contains("Recorded", reply);
            Assert.NotNull(ws.ActiveWorkspace?.ProjectState?.Identity);
            Assert.Contains("EngineOS", ws.ActiveWorkspace!.ProjectState!.Identity!.Name);
        }

        [Fact]
        public async Task ExactFailingSequence_LongDescriptionThenConfirm()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            // Exact message from the bug report
            var reply1 = await partner.SendMessageAsync(session.Id,
                "We're building EngineOS as an engineering workspace that understands what we're doing, maintains durable engineering context, and coordinates coding workers such as GitHub Copilot and Kiro. The goal is for EngineOS to own the engineering work and prepare the right handoff to the right worker.");

            // Should propose
            Assert.Contains("Should I record", reply1);

            // Exact confirmation from the bug report
            var reply2 = await partner.SendMessageAsync(session.Id, "Yes, that's right. Record it.");

            // Should confirm and persist
            Assert.Contains("Recorded", reply2);
            Assert.NotNull(ws.ActiveWorkspace?.ProjectState?.Identity);
        }

        [Fact]
        public async Task AfterEstablishment_StatusQueryReturnsIdentity()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            await partner.SendMessageAsync(session.Id,
                "We're building EngineOS as an engineering workspace that coordinates coding workers.");
            await partner.SendMessageAsync(session.Id, "Yes");

            // Now ask about the project
            var reply = await partner.SendMessageAsync(session.Id, "What is this project?");

            // The ProjectState fact in the working memory should contain the identity
            var model = await partner.GetWorkingMemoryAsync(session.Id);
            var stateFact = model!.KnownFacts.Find(f => f.Key == "ProjectState");
            Assert.NotNull(stateFact);
            Assert.Contains("EngineOS", stateFact!.Value);
        }

        // ─── No-State Conversational Path Tests ─────────────────────────────────────

        [Fact]
        public async Task NextAction_NoProjectState_RespondsConversationally()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            var reply = await partner.SendMessageAsync(session.Id, "What should we do next?");

            // Should NOT contain internal terminology
            Assert.DoesNotContain("Establish project identity", reply);
            Assert.DoesNotContain("lifecycle", reply.ToLowerInvariant());
            Assert.DoesNotContain("phase", reply.ToLowerInvariant());
            // Should ask naturally about what we're building
            Assert.Contains("building", reply.ToLowerInvariant());
        }

        [Fact]
        public async Task NextAction_NoProjectState_DoesNotExposeStateMachine()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            var reply = await partner.SendMessageAsync(session.Id, "What's the next step?");

            Assert.DoesNotContain("## Recommended Next Action", reply);
            Assert.DoesNotContain("_No implementation prompt", reply);
            Assert.DoesNotContain("Establish project identity", reply);
        }

        [Fact]
        public async Task NextAction_IdentityOnly_AsksAboutGoalNotPhase()
        {
            var (partner, ws, pss) = CreatePartnerWithFullServices();
            pss.SetProjectIdentity(new ProjectIdentity { Name = "EngineOS", Description = "Engineering workspace" });

            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id, "What should we do next?");

            // Should mention the project name
            Assert.Contains("EngineOS", reply);
            // Should ask about goal/objective — NOT about phase or lifecycle
            Assert.DoesNotContain("phase", reply.ToLowerInvariant());
            Assert.DoesNotContain("lifecycle", reply.ToLowerInvariant());
            Assert.DoesNotContain("Inception", reply);
            // Should ask about what to work on
            Assert.True(
                reply.Contains("accomplish") || reply.Contains("goal") || reply.Contains("work on") || reply.Contains("working on"),
                $"Expected reply to ask about goals naturally. Got: {reply}");
        }

        [Fact]
        public async Task NextAction_FullState_ProducesPrompt()
        {
            var (partner, ws, pss) = CreatePartnerWithFullServices();
            pss.SetProjectIdentity(new ProjectIdentity
            {
                Name = "EngineOS",
                Description = "The bridge between human and AI development",
                ProductVision = "Understand the work, keep the context, coordinate the workers"
            });
            pss.UpdateLifecyclePhase(LifecyclePhase.ActiveDevelopment, "Worker-neutral prompt generation");

            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id, "What should we do next?");

            // Should produce a real recommendation with a prompt
            Assert.Contains("## Recommended Next Action", reply);
            Assert.Contains("Worker-Ready Engineering Prompt", reply);
            Assert.Contains("EngineOS", reply);
        }

        [Fact]
        public async Task NextAction_NoState_ThenEstablish_ThenNextAction_ProducesPrompt()
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            // Ask what's next — should get conversational response
            var reply1 = await partner.SendMessageAsync(session.Id, "What should we do next?");
            Assert.DoesNotContain("## Recommended Next Action", reply1);

            // Establish project identity
            await partner.SendMessageAsync(session.Id,
                "We're building EngineOS as an engineering workspace that coordinates coding workers.");
            await partner.SendMessageAsync(session.Id, "Yes");

            // Set lifecycle
            await partner.SendMessageAsync(session.Id,
                "We're in active development focused on worker-neutral prompt generation.");
            await partner.SendMessageAsync(session.Id, "Yes");

            // Now ask again — should get a real prompt
            var reply2 = await partner.SendMessageAsync(session.Id, "What should we do next?");
            Assert.Contains("## Recommended Next Action", reply2);
            Assert.Contains("Worker-Ready Engineering Prompt", reply2);
        }

        // --- Intent Priority Tests --------------------------------------------------

        [Theory]
        [InlineData("What are we working on next?")]
        [InlineData("What should we do next?")]
        [InlineData("What's the next step?")]
        [InlineData("What should I work on next?")]
        public async Task AskingForRecommendation_DoesNotEstablishLifecycle(string message)
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            var reply = await partner.SendMessageAsync(session.Id, message);

            // Must NOT propose recording a lifecycle with "next" as focus
            Assert.DoesNotContain("Should I record the project lifecycle", reply);
            Assert.DoesNotContain("focused on: next", reply);
        }

        [Fact]
        public async Task ExplicitFocusStatement_DoesEstablishLifecycle()
        {
            var (partner, ws, pss) = CreatePartnerWithFullServices();
            pss.SetProjectIdentity(new ProjectIdentity { Name = "TestProject" });
            var session = await partner.StartSessionAsync("Hello");

            var reply = await partner.SendMessageAsync(session.Id,
                "We're in active development focused on improving the prompt generation quality.");

            // This IS a lifecycle statement — should propose
            Assert.Contains("Should I record", reply);
            Assert.Contains("prompt generation", reply.ToLowerInvariant());
            Assert.DoesNotContain("focused on: next", reply);
        }

        // ─── Workspace-Context-Aware Decision Tree Tests ────────────────────────────

        [Fact]
        public async Task NextAction_WithConnectedRepoAndInvestigation_MentionsDiscovery()
        {
            var repo = new InMemoryEngineeringModelRepository();
            var persistence = new InMemoryWorkspacePersistence();
            var ws = new WorkspaceState(persistence, new TestFingerprintService());

            // Set up workspace with a connected repo + investigation
            var workspace = new Workspace();
            var investigation = new EngineeringDiscovery.Core.Domain.Investigation.Investigation();
            // Add some type observations to simulate a discovered repo
            // (Investigation has private list, but we can set it via SetInvestigation)
            workspace.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = @"C:\projects\MyApp" });
            ws.ReplaceWorkspace(workspace);
            ws.SetInvestigation(investigation);

            var query = new EngineeringStateQuery(ws);
            var pss = new ProjectStateService(ws);
            var partner = new EngineeringPartner(repo, null, query, pss);

            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id, "What should we do next?");

            // Should reference the connected repository rather than asking what we're building from scratch
            Assert.Contains("MyApp", reply);
            Assert.Contains("accomplish", reply.ToLowerInvariant());
            // Should NOT use lifecycle terminology
            Assert.DoesNotContain("phase", reply.ToLowerInvariant());
            Assert.DoesNotContain("lifecycle", reply.ToLowerInvariant());
        }

        [Fact]
        public async Task NextAction_FullContext_ProducesPromptWithoutQuestions()
        {
            var (partner, ws, pss) = CreatePartnerWithFullServices();
            pss.SetProjectIdentity(new ProjectIdentity
            {
                Name = "EngineOS",
                Description = "The bridge between human and AI development",
                ProductVision = "Understand the work, keep the context, coordinate the workers"
            });
            pss.UpdateLifecyclePhase(LifecyclePhase.ActiveDevelopment, "Implement worker handoff loop");

            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id, "What should we do next?");

            // Should produce a real engineering prompt — no questions
            Assert.Contains("## Recommended Next Action", reply);
            Assert.Contains("Worker-Ready Engineering Prompt", reply);
            Assert.DoesNotContain("What are", reply);
            Assert.DoesNotContain("Tell me", reply);
        }

        // ─── Bug 2 regression: project name must not become engineering objective ────

        [Theory]
        [InlineData("I want to work on EngineOS.")]
        [InlineData("I want to work on EngineeringDiscovery.")]
        [InlineData("I want to work on Interview Assistant.")]
        public async Task WorkOnProjectName_DoesNotGenerateEngineeringObjective(string message)
        {
            var (partner, ws, _) = CreatePartnerWithFullServices();
            var session = await partner.StartSessionAsync("Hello");

            var reply = await partner.SendMessageAsync(session.Id, message);

            // Must NOT produce a Recommended Next Action / worker prompt from the project name
            Assert.DoesNotContain("## Recommended Next Action", reply);
            Assert.DoesNotContain("Worker-Ready Engineering Prompt", reply);
            Assert.DoesNotContain("```", reply);
        }

        [Fact]
        public async Task ExplicitEngineeringFocus_StillTriggersSetNextFocus()
        {
            var (partner, ws, pss) = CreatePartnerWithFullServices();
            pss.SetProjectIdentity(new ProjectIdentity { Name = "EngineOS" });
            pss.UpdateLifecyclePhase(LifecyclePhase.ActiveDevelopment, "testing");

            // Set up a workspace with repo so prompt generation can work
            var workspace = new Workspace();
            workspace.ImportedRepositories.Add(new ImportedRepository { RepositoryPath = @"C:\projects\EngineOS" });
            var project = new Project();
            project.State = pss.GetCurrentState()!;
            project.RepositoryPaths.Add(@"C:\projects\EngineOS");
            workspace.Projects.Add(project);
            workspace.ActiveProjectId = project.Id;
            ws.ReplaceWorkspace(workspace);

            var session = await partner.StartSessionAsync("Hello");
            var reply = await partner.SendMessageAsync(session.Id,
                "Let's work on improving the markdown rendering in ConversationHost.");

            // This IS an engineering objective (uses "let's work on" + descriptive text)
            // Should trigger SetNextFocus or produce engineering content
            // Should NOT be treated as mere project identification
            Assert.DoesNotContain("Got it — we're working on", reply);
        }
    }
}
