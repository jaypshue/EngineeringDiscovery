using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Iteration;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Tests.Tests;
using Xunit;

namespace EngineeringDiscovery.Core.Tests;

public sealed class EngineeringConversationCapabilityServiceTests
{
    [Fact]
    public async Task Context_Answers_Status_Using_Rounds_Handoff_And_Attention()
    {
        var completed = new EngineeringIteration
        {
            Goal = "Import the repository",
            Status = IterationStatus.Completed,
            CreatedUtc = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc)
        };
        var active = new EngineeringIteration
        {
            Goal = "Improve CLI integration",
            Status = IterationStatus.InProgress,
            CreatedUtc = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc),
            Steps = new List<EngineeringIterationStep>
            {
                new()
                {
                    HumanObservation = "The steering surface is ready for review.",
                    CopilotResponse = "The implementation was completed.",
                    Assessment = "Direction remains clear."
                }
            }
        };
        var project = new ProjectState
        {
            Identity = new ProjectIdentity { Name = "EngineeringDiscovery" },
            Lifecycle = new ProjectLifecycle
            {
                Phase = LifecyclePhase.ActiveDevelopment,
                CurrentFocus = "Improve CLI integration"
            },
            CurrentHandoff = new HandoffState { Objective = "Implement CLI coding-agent support." }
        };
        var state = CreateWorkspaceState(new Workspace
        {
            ProjectState = project,
            Iterations = new List<EngineeringIteration> { completed, active }
        });
        var capabilities = CreateCapabilities(state);

        var context = capabilities.GetContext();
        var result = await capabilities.TryHandleAsync("Where are we?");

        Assert.Equal("Improve CLI integration", context.CurrentDirection);
        Assert.Equal("Round 2", context.CurrentRound);
        Assert.Equal("Round 1 — Import the repository · Complete", context.LastCompleted);
        Assert.Contains("Implement CLI coding-agent support.", context.NextHandoff, StringComparison.Ordinal);
        Assert.Contains("Improve CLI integration", result!.Reply, StringComparison.Ordinal);
        Assert.Contains("Round 2", result.Reply, StringComparison.Ordinal);
        Assert.Contains("Implement CLI coding-agent support.", result.Reply, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectState", context.ToPromptText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Direction_And_Handoff_Changes_Are_Auditable_And_Persisted()
    {
        var project = new ProjectState
        {
            Identity = new ProjectIdentity { Name = "EngineeringDiscovery" },
            Lifecycle = new ProjectLifecycle
            {
                Phase = LifecyclePhase.ActiveDevelopment,
                CurrentFocus = "Improve development surface"
            },
            CurrentHandoff = new HandoffState { Objective = "Review the surface." }
        };
        var state = CreateWorkspaceState(new Workspace { ProjectState = project });
        var capabilities = CreateCapabilities(state);

        var direction = await capabilities.TryHandleAsync("Set the current direction to CLI coding-agent integration.");
        var handoff = await capabilities.TryHandleAsync("Change the next handoff to Implement Copilot CLI support.");

        Assert.True(direction!.ChangedState);
        Assert.Contains("Previous: Improve development surface", direction.Reply, StringComparison.Ordinal);
        Assert.Contains("New: CLI coding-agent integration", direction.Reply, StringComparison.Ordinal);
        Assert.True(handoff!.ChangedState);
        Assert.Contains("Previous: Review the surface", handoff.Reply, StringComparison.Ordinal);
        Assert.Contains("New: Implement Copilot CLI support", handoff.Reply, StringComparison.Ordinal);
        Assert.Equal("CLI coding-agent integration", state.ActiveWorkspace!.ProjectState!.Lifecycle!.CurrentFocus);
        Assert.Equal("Implement Copilot CLI support", state.ActiveWorkspace.ProjectState.CurrentHandoff!.Objective);
    }

    [Fact]
    public async Task Explicit_Decision_And_Handoff_Withdrawal_Are_Recorded()
    {
        var project = new ProjectState
        {
            CurrentHandoff = new HandoffState { Objective = "Build an IDE integration." }
        };
        var state = CreateWorkspaceState(new Workspace { ProjectState = project });
        var capabilities = CreateCapabilities(state);

        var decision = await capabilities.TryHandleAsync("Record that we decided not to build an IDE.");
        var cleared = await capabilities.TryHandleAsync("Stop treating that as the next handoff.");

        Assert.True(decision!.ChangedState);
        Assert.Contains("Decision recorded", decision.Reply, StringComparison.Ordinal);
        Assert.Single(state.ActiveWorkspace!.ProjectState!.Decisions);
        Assert.True(cleared!.ChangedState);
        Assert.Null(state.ActiveWorkspace.ProjectState.CurrentHandoff);
        Assert.Contains("Previous: Build an IDE integration", cleared.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partner_Routes_Conversation_Request_Through_Shared_Capabilities()
    {
        var project = new ProjectState
        {
            Lifecycle = new ProjectLifecycle
            {
                Phase = LifecyclePhase.ActiveDevelopment,
                CurrentFocus = "Improve development surface"
            }
        };
        var state = CreateWorkspaceState(new Workspace { ProjectState = project });
        var query = new EngineeringStateQuery(state);
        var projectState = new ProjectStateService(state);
        var capabilities = new EngineeringConversationCapabilityService(
            query,
            projectState,
            state,
            new EngineeringIterationService(state));
        var partner = new EngineeringPartner(
            new InMemoryEngineeringModelRepository(),
            stateQuery: query,
            projectStateService: projectState,
            capabilityService: capabilities);
        var session = await partner.StartSessionAsync(string.Empty);

        var reply = await partner.SendMessageAsync(session.Id, "Set the current direction to CLI coding-agent integration.");

        Assert.Contains("EngineOS changed", reply, StringComparison.Ordinal);
        Assert.Equal("CLI coding-agent integration", state.ActiveWorkspace!.ProjectState!.Lifecycle!.CurrentFocus);
        var model = await partner.GetWorkingMemoryAsync(session.Id);
        Assert.Contains(model!.KnownFacts, fact => fact.Key == "EngineeringContext");
        Assert.Equal("Change direction", model.KnownFacts.Last(fact => fact.Key == "LastCapabilityAction").Value);
    }

    [Fact]
    public async Task Prompt_Generation_Preserves_The_Prompt_In_The_Current_Round()
    {
        var project = new ProjectState
        {
            Identity = new ProjectIdentity { Name = "EngineeringDiscovery" },
            Lifecycle = new ProjectLifecycle
            {
                Phase = LifecyclePhase.ActiveDevelopment,
                CurrentFocus = "Prepare the next handoff"
            },
            CurrentHandoff = new HandoffState { Objective = "Prepare CLI integration." }
        };
        var round = new EngineeringIteration { Goal = "Prepare the next handoff" };
        var state = CreateWorkspaceState(new Workspace
        {
            ProjectState = project,
            Iterations = new List<EngineeringIteration> { round }
        });
        var capabilities = CreateCapabilities(state);

        var result = await capabilities.TryHandleAsync("Generate the implementation prompt.");

        Assert.True(result!.ChangedState);
        Assert.Contains("Prompt generated and preserved in Round 1", result.Reply, StringComparison.Ordinal);
        Assert.Contains("Prepare CLI integration.", result.Reply, StringComparison.Ordinal);
        Assert.Single(round.Steps);
        Assert.Contains("Prepare CLI integration.", round.Steps[0].Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Complete_Round_Uses_Shared_Iteration_Service()
    {
        var round = new EngineeringIteration { Goal = "Review the current work" };
        var state = CreateWorkspaceState(new Workspace { Iterations = new List<EngineeringIteration> { round } });
        var capabilities = CreateCapabilities(state);

        var result = await capabilities.TryHandleAsync("Mark this round complete.");

        Assert.True(result!.ChangedState);
        Assert.Equal(IterationStatus.Completed, round.Status);
        Assert.Contains("Development round completed", result.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Safe_Operation_Is_Routed_And_Reports_Actual_Result()
    {
        var gateway = new RecordingGateway(new EngineeringOperationResult(
            EngineeringOperationKind.Test,
            true,
            "38 passed · 0 failed"));
        var state = CreateWorkspaceState(new Workspace { ProjectState = new ProjectState() });
        var capabilities = CreateCapabilities(state, gateway);

        var result = await capabilities.TryHandleAsync("Run the tests.");

        Assert.True(result!.PerformedOperation);
        Assert.Equal(EngineeringOperationKind.Test, gateway.LastOperation);
        Assert.Contains("Tests succeeded", result.Reply, StringComparison.Ordinal);
        Assert.Contains("38 passed · 0 failed", result.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ambiguous_Reassessment_Does_Not_Change_State()
    {
        var round = new EngineeringIteration { Goal = "Review the current work" };
        var state = CreateWorkspaceState(new Workspace { Iterations = new List<EngineeringIteration> { round } });
        var capabilities = CreateCapabilities(state);

        var result = await capabilities.TryHandleAsync("I don't agree with that assessment. Reconsider it.");

        Assert.True(result!.RequiresClarification);
        Assert.False(result.ChangedState);
        Assert.Equal(IterationStatus.InProgress, round.Status);
        Assert.Contains("Nothing was changed", result.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task External_Research_Is_Explicitly_Unavailable_And_Does_Not_Execute()
    {
        var state = CreateWorkspaceState(new Workspace { ProjectState = new ProjectState() });
        var capabilities = CreateCapabilities(state);

        var result = await capabilities.TryHandleAsync("Look up the current GitHub Copilot CLI documentation.");

        Assert.NotNull(result);
        Assert.False(result!.PerformedOperation);
        Assert.Contains("no web request was made", result.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Partner_ReadOnly_Path_Does_Not_Dispatch_State_Changes()
    {
        var project = new ProjectState
        {
            Lifecycle = new ProjectLifecycle
            {
                Phase = LifecyclePhase.ActiveDevelopment,
                CurrentFocus = "Keep the current direction"
            }
        };
        var state = CreateWorkspaceState(new Workspace { ProjectState = project });
        var query = new EngineeringStateQuery(state);
        var projectState = new ProjectStateService(state);
        var capabilities = new EngineeringConversationCapabilityService(
            query,
            projectState,
            state,
            new EngineeringIterationService(state));
        var partner = new EngineeringPartner(
            new InMemoryEngineeringModelRepository(),
            stateQuery: query,
            projectStateService: projectState,
            capabilityService: capabilities);
        var session = await partner.StartSessionAsync(string.Empty);

        var reply = await partner.SendReadOnlyMessageAsync(
            session.Id,
            "Set the current direction to an unauthorized change.");

        Assert.Contains("read-only", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Keep the current direction", project.Lifecycle!.CurrentFocus);
        Assert.Empty(project.Decisions);
    }

    private static EngineeringConversationCapabilityService CreateCapabilities(
        WorkspaceState state,
        IEngineeringOperationGateway gateway = null)
    {
        var query = new EngineeringStateQuery(state);
        var projectState = new ProjectStateService(state);
        var iterations = new EngineeringIterationService(state);
        return new EngineeringConversationCapabilityService(query, projectState, state, iterations, gateway);
    }

    private static WorkspaceState CreateWorkspaceState(Workspace workspace)
    {
        var state = new WorkspaceState(new InMemoryWorkspacePersistence(), new TestFingerprintService());
        state.ReplaceWorkspace(workspace);
        return state;
    }

    private sealed class RecordingGateway : IEngineeringOperationGateway
    {
        private readonly EngineeringOperationResult _result;

        public RecordingGateway(EngineeringOperationResult result) => _result = result;

        public EngineeringOperationKind? LastOperation { get; private set; }

        public Task<EngineeringOperationResult> ExecuteAsync(
            EngineeringOperationKind operation,
            CancellationToken cancellationToken = default)
        {
            LastOperation = operation;
            return Task.FromResult(_result);
        }
    }

    [Fact]
    public async Task ReadOnly_Status_Question_Does_Not_Change_Authoritative_State()
    {
        var project = new ProjectState
        {
            Lifecycle = new ProjectLifecycle
            {
                Phase = LifecyclePhase.ActiveDevelopment,
                CurrentFocus = "Understand the loaded repository"
            },
            CurrentHandoff = new HandoffState { Objective = "Review the recorded evidence." }
        };
        var state = CreateWorkspaceState(new Workspace { ProjectState = project });
        var capabilities = CreateCapabilities(state);

        var result = await capabilities.TryHandleAsync("Where are we?");

        Assert.NotNull(result);
        Assert.False(result!.ChangedState);
        Assert.Equal("Understand the loaded repository", project.Lifecycle!.CurrentFocus);
        Assert.Equal("Review the recorded evidence.", project.CurrentHandoff!.Objective);
        Assert.Empty(project.Decisions);
    }

    [Fact]
    public async Task Repository_Question_Reports_Missing_Context_Without_Fabricating_It()
    {
        var state = CreateWorkspaceState(new Workspace());
        var capabilities = CreateCapabilities(state);

        var result = await capabilities.TryHandleAsync("What does EngineOS know about this repository?");

        Assert.NotNull(result);
        Assert.False(result!.ChangedState);
        Assert.Contains("No repository loaded.", result.Reply, StringComparison.Ordinal);
        Assert.Contains("Investigation evidence is not available.", result.Reply, StringComparison.Ordinal);
    }
}