using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Iteration;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.ViewModels;
using Moq;
using Xunit;

namespace EngineeringDiscovery.Wpf.Tests;

public sealed class EngineeringSteeringViewModelTests
{
    [Fact]
    public void Projection_Maps_Direction_Handoff_Active_Round_And_Completed_Round()
    {
        var completed = new EngineeringIteration
        {
            Goal = "Import the repository",
            Status = IterationStatus.Completed,
            CreatedUtc = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc)
        };
        var active = new EngineeringIteration
        {
            Goal = "Make steering primary",
            Status = IterationStatus.InProgress,
            CreatedUtc = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc)
        };
        active.Steps.Add(new EngineeringIterationStep { NextAction = "Review the steering surface." });

        var workspace = new Workspace
        {
            ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity { Name = "EngineeringDiscovery" },
                Lifecycle = new ProjectLifecycle
                {
                    Phase = LifecyclePhase.ActiveDevelopment,
                    CurrentFocus = "Make steering primary"
                },
                CurrentHandoff = new HandoffState { Objective = "Review the steering surface." }
            },
            Iterations = new List<EngineeringIteration> { active, completed }
        };
        var state = CreateWorkspaceState();
        state.ReplaceWorkspace(workspace);

        var query = CreateQuery(
            workspace.ProjectState.Lifecycle,
            workspace.ProjectState.CurrentHandoff,
            Array.Empty<KnownIssue>(),
            Array.Empty<WorkerEngagement>());
        var vm = new EngineeringSteeringViewModel(query.Object, state);

        Assert.Equal("Make steering primary", vm.CurrentDirection);
        Assert.Equal("Round 2", vm.CurrentRound);
        Assert.Equal("Make steering primary", vm.CurrentRoundGoal);
        Assert.Equal("● In Progress", vm.CurrentRoundStatus);
        Assert.Equal("Round 1 — Import the repository · ✓ Complete", vm.LastCompleted);
        Assert.Equal("Review the steering surface.", vm.NextHandoff);
        Assert.Equal("Ready", vm.HandoffStatus);
        Assert.Equal("High — ready to continue", vm.Confidence);
        Assert.Equal("🟢 No attention required", vm.HumanAttention);
        Assert.True(vm.DevelopmentRounds[1].IsCurrent);
        Assert.False(vm.DevelopmentRounds[1].IsHistorical);
        Assert.False(vm.DevelopmentRounds[0].IsCurrent);
        Assert.True(vm.DevelopmentRounds[0].IsHistorical);
        Assert.True(vm.GeneratePromptCommand.CanExecute(null));

        vm.GeneratePromptCommand.Execute(null);
        Assert.Contains("Review the steering surface.", vm.PromptText, StringComparison.Ordinal);
        Assert.Contains("Review the steering surface.", Assert.Single(active.Steps).Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Projection_Uses_Supported_Recommendation_Without_Calling_It_A_Ready_Handoff()
    {
        var projectState = new ProjectState
        {
            Identity = new ProjectIdentity { Name = "EngineeringDiscovery" },
            Lifecycle = new ProjectLifecycle
            {
                Phase = LifecyclePhase.ActiveDevelopment,
                CurrentFocus = "Inspect evidence"
            }
        };
        var workspace = new Workspace { ProjectState = projectState };
        var state = CreateWorkspaceState();
        state.ReplaceWorkspace(workspace);

        var query = CreateQuery(projectState.Lifecycle, null, Array.Empty<KnownIssue>(), Array.Empty<WorkerEngagement>());
        var vm = new EngineeringSteeringViewModel(query.Object, state);

        Assert.Equal("No clear next handoff established.", vm.NextHandoff);
        Assert.Equal("Human decision required", vm.HandoffStatus);
        Assert.Equal("Low — next handoff is not established", vm.Confidence);
        Assert.Contains("Establish the next handoff", vm.HumanAttention, StringComparison.Ordinal);
        Assert.False(vm.GeneratePromptCommand.CanExecute(null));
    }

    [Fact]
    public void Projection_Requests_Human_Intervention_For_Critical_Issue()
    {
        var projectState = new ProjectState
        {
            Identity = new ProjectIdentity { Name = "EngineeringDiscovery" },
            Lifecycle = new ProjectLifecycle
            {
                Phase = LifecyclePhase.ActiveDevelopment,
                CurrentFocus = "Continue development"
            },
            CurrentHandoff = new HandoffState { Objective = "Continue development" }
        };
        var workspace = new Workspace { ProjectState = projectState };
        var state = CreateWorkspaceState();
        state.ReplaceWorkspace(workspace);

        var query = CreateQuery(
            projectState.Lifecycle,
            projectState.CurrentHandoff,
            new[] { new KnownIssue { Title = "Build is blocked", Severity = IssueSeverity.Critical } },
            Array.Empty<WorkerEngagement>());
        var vm = new EngineeringSteeringViewModel(query.Object, state);

        Assert.Equal("Low — human direction required", vm.Confidence);
        Assert.Contains("Critical issue: Build is blocked", vm.HumanAttention, StringComparison.Ordinal);
    }

    [Fact]
    public void RoundProjection_Exposes_Recorded_Review_And_Does_Not_Fabricate_Missing_Evidence()
    {
        var round = new EngineeringIteration
        {
            Goal = "Review the implementation",
            Status = IterationStatus.InProgress,
            Steps = new List<EngineeringIterationStep>
            {
                new()
                {
                    Prompt = "Implement the requested change.",
                    CopilotResponse = "Implemented the requested change.",
                    HumanObservation = "The implementation is ready for review.",
                    Assessment = "Direction remains clear."
                }
            }
        };
        var workspace = new Workspace { Iterations = new List<EngineeringIteration> { round } };
        var state = CreateWorkspaceState();
        state.ReplaceWorkspace(workspace);
        var query = CreateQuery(null, null, Array.Empty<KnownIssue>(), Array.Empty<WorkerEngagement>());

        var vm = new EngineeringSteeringViewModel(query.Object, state);
        var projected = Assert.Single(vm.DevelopmentRounds);

        Assert.Equal("Prompt preserved", projected.PromptStatus);
        Assert.Equal("The implementation is ready for review.", projected.ReviewSummary);
        Assert.Equal("Implemented the requested change.", projected.AgentResponse);
        Assert.Equal("Direction remains clear.", projected.Assessment);
        Assert.Equal("The implementation is ready for review.", vm.CurrentRoundSummary);
        Assert.Equal("Implemented the requested change.", vm.CurrentRoundAgentResponse);
        Assert.Equal("Direction remains clear.", vm.CurrentRoundAssessment);
        Assert.Contains("Not captured", vm.CurrentRoundChanges, StringComparison.Ordinal);
        Assert.Contains("Not captured", vm.CurrentRoundTests, StringComparison.Ordinal);
        Assert.Contains("Not captured", vm.CurrentRoundProblems, StringComparison.Ordinal);
    }

    private static Mock<IEngineeringStateQuery> CreateQuery(
        ProjectLifecycle? lifecycle,
        HandoffState? handoff,
        IReadOnlyList<KnownIssue> issues,
        IReadOnlyList<WorkerEngagement> engagements)
    {
        var query = new Mock<IEngineeringStateQuery>();
        query.Setup(item => item.GetLifecycle()).Returns(lifecycle);
        query.Setup(item => item.GetCurrentHandoff()).Returns(handoff);
        query.Setup(item => item.GetResumePoint()).Returns((ResumePoint?)null);
        query.Setup(item => item.GetOpenIssues()).Returns(issues);
        query.Setup(item => item.GetRecentEngagements(20)).Returns(engagements);
        return query;
    }

    private static WorkspaceState CreateWorkspaceState() =>
        new(new InMemoryWorkspacePersistence(), new TestRepoFingerprintService());
}
