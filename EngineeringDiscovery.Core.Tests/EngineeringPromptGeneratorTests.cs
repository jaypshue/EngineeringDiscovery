using System;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Activity;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class EngineeringPromptGeneratorTests
    {
        private static ProjectState CreateFullState(string focus = "Worker-neutral prompt generation")
        {
            var state = new ProjectState
            {
                Identity = new ProjectIdentity
                {
                    Name = "EngineOS",
                    Description = "The bridge between human and AI software development.",
                    ProductVision = "EngineOS understands the work, maintains engineering context, and coordinates coding workers."
                },
                Lifecycle = new ProjectLifecycle
                {
                    Phase = LifecyclePhase.ActiveDevelopment,
                    CurrentFocus = focus
                },
                ResumePoint = new ResumePoint
                {
                    Summary = $"Project: EngineOS, Phase: ActiveDevelopment, Focus: {focus}",
                    NextRecommendedAction = $"Continue active development on: {focus}",
                    Rationale = "Based on current lifecycle phase and project focus."
                }
            };
            return state;
        }

        // ─── GeneratePrompt tests ───────────────────────────────────────────────────

        [Fact]
        public void GeneratePrompt_ContainsObjective()
        {
            var handoff = new HandoffState { Objective = "Implement worker handoff loop" };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, null);
            Assert.Contains("Implement worker handoff loop", prompt);
        }

        [Fact]
        public void GeneratePrompt_ContainsProjectContext()
        {
            var state = CreateFullState();
            var handoff = new HandoffState { Objective = "Next task" };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, state);
            Assert.Contains("EngineOS", prompt);
            Assert.Contains("ActiveDevelopment", prompt);
            Assert.Contains("Worker-neutral prompt generation", prompt);
            Assert.Contains("bridge between human and AI", prompt);
        }

        [Fact]
        public void GeneratePrompt_ContainsGoalSection()
        {
            var handoff = new HandoffState { Objective = "My objective", Context = "Here is the background." };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, null);
            Assert.Contains("## Goal", prompt);
            Assert.Contains("My objective", prompt);
            Assert.Contains("Here is the background", prompt);
        }

        [Fact]
        public void GeneratePrompt_ContainsConstraints()
        {
            var handoff = new HandoffState
            {
                Objective = "Test",
                Constraints = new List<string> { "Do not modify unrelated code", "Keep changes minimal" }
            };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, null);
            Assert.Contains("Do not modify unrelated code", prompt);
            Assert.Contains("Keep changes minimal", prompt);
        }

        [Fact]
        public void GeneratePrompt_ContainsAcceptanceCriteria()
        {
            var handoff = new HandoffState
            {
                Objective = "Test",
                AcceptanceCriteria = "All tests pass and the feature works end-to-end."
            };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, null);
            Assert.Contains("All tests pass and the feature works end-to-end", prompt);
        }

        [Fact]
        public void GeneratePrompt_ContainsRelevantFiles()
        {
            var handoff = new HandoffState
            {
                Objective = "Test",
                RelevantFiles = new List<string> { "Services/ProjectStateService.cs", "Domain/ProjectState/HandoffState.cs" }
            };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, null);
            Assert.Contains("Services/ProjectStateService.cs", prompt);
            Assert.Contains("Domain/ProjectState/HandoffState.cs", prompt);
        }

        [Fact]
        public void GeneratePrompt_ContainsPreviousWork()
        {
            var handoff = new HandoffState
            {
                Objective = "Retry task",
                PreviousEngagementSummary = "Worker attempted but build failed due to missing dependency"
            };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, null);
            Assert.Contains("Worker attempted but build failed", prompt);
        }

        [Fact]
        public void GeneratePrompt_ContainsVerificationSection()
        {
            var handoff = new HandoffState { Objective = "Any task" };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, null);
            Assert.Contains("## Verification", prompt);
            Assert.Contains("Build the affected project", prompt);
            Assert.Contains("Add/update tests", prompt);
        }

        [Fact]
        public void GeneratePrompt_ContainsScopeSection()
        {
            var handoff = new HandoffState { Objective = "Any task" };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, null);
            Assert.Contains("## Scope", prompt);
            Assert.Contains("Do not redesign unrelated architecture", prompt);
        }

        [Fact]
        public void GeneratePrompt_IsWorkerNeutral()
        {
            var state = CreateFullState();
            var handoff = new HandoffState { Objective = "Implement feature" };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, state);
            Assert.DoesNotContain("Kiro", prompt);
            Assert.DoesNotContain("Copilot", prompt);
            Assert.DoesNotContain("Codex", prompt);
        }

        [Fact]
        public void GeneratePrompt_IncludesEngineeringDecisions()
        {
            var state = CreateFullState();
            state.Decisions.Add(new EngineeringDecision { Statement = "Use existing IWorkspacePersistence for all storage", Status = DecisionStatus.Accepted, CreatedUtc = DateTime.UtcNow });
            state.Decisions.Add(new EngineeringDecision { Statement = "Worker architecture must be agent-neutral", Status = DecisionStatus.Accepted, CreatedUtc = DateTime.UtcNow });

            var handoff = new HandoffState { Objective = "Implement feature" };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, state);
            Assert.Contains("Engineering Decisions", prompt);
            Assert.Contains("agent-neutral", prompt);
            Assert.Contains("IWorkspacePersistence", prompt);
        }

        [Fact]
        public void GeneratePrompt_IncludesKnownIssues()
        {
            var state = CreateFullState();
            state.KnownIssues.Add(new KnownIssue { Title = "ConversationComposer component reference is broken (RZ10012)" });
            state.KnownIssues.Add(new KnownIssue { Title = "DevB worktree is stale and should be removed" });

            var handoff = new HandoffState { Objective = "Fix issue" };
            var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, state);
            Assert.Contains("Known Issues", prompt);
            Assert.Contains("ConversationComposer", prompt);
        }

        // ─── DetermineNextAction tests ──────────────────────────────────────────────

        [Fact]
        public void DetermineNextAction_NoState_RecommendsEstablishIdentity()
        {
            var (rec, _, handoff) = EngineeringPromptGenerator.DetermineNextAction(null);
            Assert.Contains("identity", rec, StringComparison.OrdinalIgnoreCase);
            Assert.Null(handoff);
        }

        [Fact]
        public void DetermineNextAction_NoIdentity_RecommendsEstablishIdentity()
        {
            var state = new ProjectState();
            var (rec, _, handoff) = EngineeringPromptGenerator.DetermineNextAction(state);
            Assert.Contains("identity", rec, StringComparison.OrdinalIgnoreCase);
            Assert.Null(handoff);
        }

        [Fact]
        public void DetermineNextAction_NoLifecycle_RecommendsSetPhase()
        {
            var state = new ProjectState { Identity = new ProjectIdentity { Name = "Test" } };
            var (rec, _, _) = EngineeringPromptGenerator.DetermineNextAction(state);
            Assert.Contains("lifecycle", rec, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DetermineNextAction_ActiveDevelopment_ReturnsHandoffWithFocus()
        {
            var state = CreateFullState("Implement the manual worker handoff loop");
            var (rec, rationale, handoff) = EngineeringPromptGenerator.DetermineNextAction(state);
            Assert.NotNull(handoff);
            Assert.Contains("worker handoff", handoff!.Objective, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(handoff.Constraints);
            Assert.Contains("EngineOS", handoff.Context);
        }

        [Fact]
        public void DetermineNextAction_IncludesRecentCompletedWork()
        {
            var state = CreateFullState();
            state.WorkerEngagements.Add(new WorkerEngagement
            {
                WorkerName = "Worker1",
                TaskDescription = "Merge DevB into main codebase",
                Outcome = EngagementOutcome.Completed,
                Acceptance = AcceptanceStatus.Accepted,
                CompletedUtc = DateTime.UtcNow.AddHours(-2),
                StartedUtc = DateTime.UtcNow.AddHours(-3)
            });
            var (_, _, handoff) = EngineeringPromptGenerator.DetermineNextAction(state);
            Assert.NotNull(handoff);
            Assert.Contains("Merge DevB", handoff!.Context);
            Assert.Contains("do not repeat", handoff.Context, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DetermineNextAction_FailedEngagement_RecommendsRetry()
        {
            var state = CreateFullState();
            state.WorkerEngagements.Add(new WorkerEngagement
            {
                WorkerName = "Worker1",
                TaskDescription = "Fix the build pipeline",
                Outcome = EngagementOutcome.Failed,
                Summary = "Build still failing after attempted fix — wrong dependency version",
                FilesChanged = new List<string> { "Directory.Packages.props" },
                StartedUtc = DateTime.UtcNow.AddHours(-1)
            });
            var (rec, rationale, handoff) = EngineeringPromptGenerator.DetermineNextAction(state);
            Assert.Contains("Fix the build pipeline", rec);
            Assert.NotNull(handoff);
            Assert.Contains("previous worker", handoff!.Context, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Directory.Packages.props", string.Join(",", handoff.RelevantFiles));
        }

        [Fact]
        public void DetermineNextAction_CompletedEngagement_DoesNotRepropose()
        {
            var state = CreateFullState("Worker handoff");
            state.WorkerEngagements.Add(new WorkerEngagement
            {
                WorkerName = "Worker1",
                TaskDescription = "Implement worker models",
                Outcome = EngagementOutcome.Completed,
                Acceptance = AcceptanceStatus.Accepted,
                StartedUtc = DateTime.UtcNow.AddDays(-1)
            });
            var (rec, _, _) = EngineeringPromptGenerator.DetermineNextAction(state);
            Assert.DoesNotContain("Implement worker models", rec);
        }

        [Fact]
        public void DetermineNextAction_HasConcreteAcceptanceCriteria()
        {
            var state = CreateFullState("Implement prompt generation");
            var (_, _, handoff) = EngineeringPromptGenerator.DetermineNextAction(state);
            Assert.NotNull(handoff);
            Assert.Contains("objective", handoff!.AcceptanceCriteria, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("tests", handoff.AcceptanceCriteria, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("zero errors", handoff.AcceptanceCriteria, StringComparison.OrdinalIgnoreCase);
        }

        // ─── DetermineActionForFocus tests ──────────────────────────────────────────

        [Fact]
        public void DetermineActionForFocus_ProducesHandoffWithExplicitObjective()
        {
            var state = CreateFullState();
            var (rec, _, handoff) = EngineeringPromptGenerator.DetermineActionForFocus(
                "Implement the IWorkerAdapter interface for agent-neutral worker coordination", state);
            Assert.Equal("Implement the IWorkerAdapter interface for agent-neutral worker coordination", rec);
            Assert.NotNull(handoff);
            Assert.Equal(rec, handoff.Objective);
        }

        [Fact]
        public void DetermineActionForFocus_IncludesProjectContextInHandoff()
        {
            var state = CreateFullState();
            var (_, _, handoff) = EngineeringPromptGenerator.DetermineActionForFocus("Build the handoff UI", state);
            Assert.Contains("EngineOS", handoff.Context);
        }

        [Fact]
        public void DetermineActionForFocus_IncludesDecisionConstraints()
        {
            var state = CreateFullState();
            state.Decisions.Add(new EngineeringDecision { Statement = "No vendor-specific logic in Core", Status = DecisionStatus.Accepted, CreatedUtc = DateTime.UtcNow });
            var (_, _, handoff) = EngineeringPromptGenerator.DetermineActionForFocus("Implement worker adapter", state);
            Assert.Contains("No vendor-specific logic", string.Join(" | ", handoff.Constraints));
        }
    }
}
