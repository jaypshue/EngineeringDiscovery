using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Core.Domain.Iteration;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Models;
using EngineeringDiscovery.Wpf.Services;
using EngineeringDiscovery.Wpf.ViewModels;
using Moq;
using Xunit;

namespace EngineeringDiscovery.Wpf.Tests;

public sealed class CodingAgentViewModelTests
{
    [Fact]
    public async Task SendToCodingAgent_RequiresGeneratedPromptRepositoryAndConfiguredProvider()
    {
        var root = CreateTempDirectory();
        try
        {
            using var withoutProvider = await CreateViewModelAsync(root, null);
            Assert.False(withoutProvider.Steering.SendToCodingAgentCommand.CanExecute(null));

            var provider = new FakeHandoffService();
            using var vm = await CreateViewModelAsync(root, provider);
            Assert.False(vm.Steering.SendToCodingAgentCommand.CanExecute(null));

            vm.Steering.GeneratePromptCommand.Execute(null);

            Assert.True(vm.Steering.HasPromptArtifact);
            Assert.True(vm.Steering.HasRepository);
            Assert.True(vm.Steering.SendToCodingAgentCommand.CanExecute(null));
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task SendToCodingAgent_ReportsWorkingCompletionAndCapturedResponse()
    {
        var root = CreateTempDirectory();
        try
        {
            var provider = new FakeHandoffService();
            using var vm = await CreateViewModelAsync(root, provider);
            vm.Steering.GeneratePromptCommand.Execute(null);
            var prompt = vm.Steering.PromptText;

            var command = Assert.IsType<CommunityToolkit.Mvvm.Input.AsyncRelayCommand>(vm.Steering.SendToCodingAgentCommand);
            var execution = command.ExecuteAsync(null);
            await WaitUntilAsync(() => vm.Steering.IsAgentWorking);
            Assert.Contains("Working", vm.Steering.AgentStatus, StringComparison.Ordinal);
            Assert.Single(provider.Requests);
            Assert.Equal(prompt, provider.Requests[0].Prompt);

            provider.Complete(SuccessArtifact(prompt, "Agent completed the implementation."));
            await execution;

            Assert.False(vm.Steering.IsAgentWorking);
            Assert.Contains("Completed", vm.Steering.AgentStatus, StringComparison.Ordinal);
            Assert.Equal("Agent completed the implementation.", vm.Steering.AgentResponseText);
            Assert.Equal("Completed", vm.Steering.AgentResponseStatus);
            Assert.Contains("current development round", vm.Steering.AgentResponseAssociation, StringComparison.OrdinalIgnoreCase);
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task SendToCodingAgent_ReportsProviderFailureAndPreservesFailureState()
    {
        var root = CreateTempDirectory();
        try
        {
            var provider = new FakeHandoffService();
            using var vm = await CreateViewModelAsync(root, provider);
            vm.Steering.GeneratePromptCommand.Execute(null);
            var command = Assert.IsType<CommunityToolkit.Mvvm.Input.AsyncRelayCommand>(vm.Steering.SendToCodingAgentCommand);
            var execution = command.ExecuteAsync(null);
            await WaitUntilAsync(() => vm.Steering.IsAgentWorking);
            provider.Complete(FailedArtifact(vm.Steering.PromptText, "Copilot authentication unavailable."));
            await execution;

            Assert.False(vm.Steering.IsAgentWorking);
            Assert.Contains("Failed", vm.Steering.AgentStatus, StringComparison.Ordinal);
            Assert.Contains("authentication unavailable", vm.Steering.AgentResponseText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Failed", vm.Steering.AgentResponseStatus, StringComparison.Ordinal);
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task CancelCodingAgent_ReportsCancellationState()
    {
        var root = CreateTempDirectory();
        try
        {
            var provider = new FakeHandoffService { WaitForCancellation = true };
            using var vm = await CreateViewModelAsync(root, provider);
            vm.Steering.GeneratePromptCommand.Execute(null);
            var command = Assert.IsType<CommunityToolkit.Mvvm.Input.AsyncRelayCommand>(vm.Steering.SendToCodingAgentCommand);
            var execution = command.ExecuteAsync(null);
            await WaitUntilAsync(() => vm.Steering.IsAgentWorking);

            Assert.True(vm.Steering.CancelCodingAgentCommand.CanExecute(null));
            vm.Steering.CancelCodingAgentCommand.Execute(null);
            await execution;

            Assert.False(vm.Steering.IsAgentWorking);
            Assert.Contains("Cancelled", vm.Steering.AgentStatus, StringComparison.Ordinal);
            Assert.Equal("Cancelled", vm.Steering.AgentResponseStatus);
        }
        finally { DeleteTempDirectory(root); }
    }

    private static async Task<DevelopmentSurfaceViewModel> CreateViewModelAsync(
        string root,
        FakeHandoffService? handoffService)
    {
        var query = new Mock<IEngineeringStateQuery>();
        query.Setup(item => item.GetWorkspaceContext()).Returns(new WorkspaceContext
        {
            HasRepository = true,
            RepositoryName = "CodingAgentFixture",
            RepositoryPath = root
        });
        query.Setup(item => item.GetLifecycle()).Returns(new ProjectLifecycle
        {
            Phase = LifecyclePhase.ActiveDevelopment,
            CurrentFocus = "Send the implementation prompt to the coding agent."
        });
        query.Setup(item => item.GetCurrentHandoff()).Returns(new HandoffState
        {
            Objective = "Send the implementation prompt to the coding agent.",
            Context = "Use the existing generated prompt.",
            AcceptanceCriteria = "Capture the response."
        });
        query.Setup(item => item.GetOpenIssues()).Returns(Array.Empty<KnownIssue>());
        query.Setup(item => item.GetRecentEngagements(20)).Returns(Array.Empty<WorkerEngagement>());

        var state = new WorkspaceState(new InMemoryWorkspacePersistence(), new TestRepoFingerprintService());
        state.ReplaceWorkspace(new Workspace
        {
            RepositoryPath = root,
            ProjectState = new ProjectState
            {
                Identity = new ProjectIdentity { Name = "CodingAgentFixture" },
                Lifecycle = new ProjectLifecycle { Phase = LifecyclePhase.ActiveDevelopment, CurrentFocus = "Handoff" }
            },
            Iterations = new List<EngineeringIteration> { new() { Goal = "Coding-agent handoff" } }
        });

        var files = new Mock<IRepositoryFileService>();
        files.Setup(item => item.GetTreeAsync(root, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepositoryFileNode("CodingAgentFixture", root, string.Empty, true));
        var changes = new Mock<IGitChangesService>();
        changes.Setup(item => item.GetChangesAsync(root, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitChange>());

        var vm = new DevelopmentSurfaceViewModel(
            query.Object,
            state,
            files.Object,
            changes.Object,
            new Mock<IDevelopmentCommandService>().Object,
            handoffService);
        await vm.InitializeAsync();
        return vm;
    }

    private static CodingAgentResponseArtifact SuccessArtifact(string prompt, string response) => new()
    {
        Provider = "github-copilot-cli",
        Prompt = prompt,
        Response = response,
        ExitCode = 0,
        Status = CodingAgentExecutionStatus.Succeeded,
        StartedUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
        CompletedUtc = DateTimeOffset.UtcNow
    };

    private static CodingAgentResponseArtifact FailedArtifact(string prompt, string reason) => new()
    {
        Provider = "github-copilot-cli",
        Prompt = prompt,
        FailureReason = reason,
        Status = CodingAgentExecutionStatus.Failed,
        StartedUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
        CompletedUtc = DateTimeOffset.UtcNow
    };

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (var attempt = 0; attempt < 50 && !predicate(); attempt++)
            await Task.Delay(10);
        Assert.True(predicate(), "The expected asynchronous state was not reached.");
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "EngineOS-CodingAgent-Wpf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }

    private sealed class FakeHandoffService : ICodingAgentHandoffService
    {
        private readonly TaskCompletionSource<CodingAgentHandoffResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<CodingAgentHandoffRequest> Requests { get; } = new();
        public bool WaitForCancellation { get; set; }

        public async Task<CodingAgentHandoffResult> SubmitAsync(
            CodingAgentHandoffRequest request,
            Action<CodingAgentOutputChunk>? output,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            output?.Invoke(new CodingAgentOutputChunk("agent is working", false, DateTimeOffset.UtcNow));
            if (WaitForCancellation)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return new CodingAgentHandoffResult(
                        new CodingAgentResponseArtifact
                        {
                            Provider = "github-copilot-cli",
                            Prompt = request.Prompt,
                            Status = CodingAgentExecutionStatus.Cancelled,
                            StartedUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
                            CompletedUtc = DateTimeOffset.UtcNow,
                            FailureReason = "Cancelled by test."
                        },
                        null,
                        null,
                        false,
                        "Cancelled by test.");
                }
            }

            return await _completion.Task;
        }

        public void Complete(CodingAgentResponseArtifact artifact) =>
            _completion.TrySetResult(new CodingAgentHandoffResult(
                artifact,
                Guid.NewGuid(),
                Guid.NewGuid(),
                true,
                null));
    }
}
