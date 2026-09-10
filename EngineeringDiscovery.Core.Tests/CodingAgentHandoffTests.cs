using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Iteration;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests;

public sealed class CodingAgentHandoffTests
{
    [Fact]
    public void CopilotConfiguration_UsesTheControlledDefaultExecutableAndWritePermission()
    {
        var configuration = CodingAgentProviderConfiguration.GitHubCopilot();

        Assert.Equal("github-copilot-cli", configuration.ProviderId);
        Assert.Equal("copilot", configuration.Executable);
        Assert.Contains("write", configuration.AllowedTools);
        Assert.Equal(TimeSpan.FromMinutes(15), configuration.DefaultTimeout);
    }

    [Fact]
    public async Task CopilotProvider_UsesProgrammaticPromptAndStreamsProviderOutput()
    {
        var runner = new RecordingProcessRunner
        {
            Result = SuccessResult("agent response")
        };
        var provider = new CopilotCodingAgentProvider(runner);
        var chunks = new List<CodingAgentOutputChunk>();

        var availability = await provider.CheckAvailabilityAsync();
        var result = await provider.ExecuteAsync(
            new CodingAgentRequest("Implement this exact task.", "C:\\repo", TimeSpan.FromMinutes(2)),
            new Action<CodingAgentOutputChunk>(chunks.Add));

        Assert.True(availability.IsAvailable);
        Assert.Equal(CodingAgentExecutionStatus.Succeeded, result.Status);
        Assert.Contains("-p", runner.StartInfos[^1].Arguments);
        Assert.Contains("Implement this exact task.", runner.StartInfos[^1].Arguments);
        Assert.Contains("-s", runner.StartInfos[^1].Arguments);
        Assert.Contains("--no-ask-user", runner.StartInfos[^1].Arguments);
        Assert.Contains("write", runner.StartInfos[^1].Arguments);
        Assert.Equal("C:\\repo", runner.StartInfos[^1].WorkingDirectory);
    }

    [Fact]
    public async Task Handoff_RejectsMissingRepositoryWithoutStartingProvider()
    {
        var provider = new FakeProvider { Availability = new CodingAgentProviderAvailability(true, null) };
        var service = new CodingAgentHandoffService(provider, CreateWorkspaceState());

        var result = await service.SubmitAsync(new CodingAgentHandoffRequest("prompt", "C:\\does-not-exist"), null);

        Assert.Equal(CodingAgentExecutionStatus.Failed, result.Artifact.Status);
        Assert.Contains("does not exist", result.Artifact.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.False(provider.ExecuteCalled);
    }

    [Fact]
    public async Task Handoff_ReportsUnavailableProviderHonestly()
    {
        var root = CreateTempDirectory();
        try
        {
            var provider = new FakeProvider
            {
                Availability = new CodingAgentProviderAvailability(false, "Copilot authentication is unavailable.")
            };
            var service = new CodingAgentHandoffService(provider, CreateWorkspaceState(root));

            var result = await service.SubmitAsync(new CodingAgentHandoffRequest("prompt", root), null);

            Assert.Equal(CodingAgentExecutionStatus.Failed, result.Artifact.Status);
            Assert.Contains("authentication is unavailable", result.Artifact.FailureReason, StringComparison.OrdinalIgnoreCase);
            Assert.False(provider.ExecuteCalled);
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task Handoff_PersistsSuccessfulResponseAndAssociatesItWithTheCurrentRoundStep()
    {
        var root = CreateTempDirectory();
        try
        {
            var state = CreateWorkspaceState(root);
            var round = Assert.Single(state.ActiveWorkspace!.Iterations);
            var step = new EngineeringIterationStep { Prompt = "Implement the exact task." };
            round.Steps.Add(step);
            var provider = new FakeProvider
            {
                Result = SuccessResult("Implemented the exact task.")
            };
            var service = new CodingAgentHandoffService(provider, state);

            var result = await service.SubmitAsync(new CodingAgentHandoffRequest(step.Prompt, root), null);

            Assert.Equal(CodingAgentExecutionStatus.Succeeded, result.Artifact.Status);
            Assert.True(result.ArtifactPersisted);
            Assert.Equal(round.Id, result.AssociatedRoundId);
            Assert.Equal(step.Id, result.AssociatedStepId);
            Assert.Same(result.Artifact, step.AgentResponseArtifact);
            Assert.Equal("Implemented the exact task.", step.CopilotResponse);
            Assert.Equal("Implemented the exact task.", step.AgentResponseArtifact!.Response);
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task Handoff_DoesNotPersistWhenPromptIdentityIsStale()
    {
        var root = CreateTempDirectory();
        try
        {
            var state = CreateWorkspaceState(root);
            var round = Assert.Single(state.ActiveWorkspace!.Iterations);
            var step = new EngineeringIterationStep { Prompt = "Current prompt." };
            round.Steps.Add(step);
            var provider = new FakeProvider();
            var service = new CodingAgentHandoffService(provider, state);

            var result = await service.SubmitAsync(
                new CodingAgentHandoffRequest(
                    "Stale prompt.",
                    root,
                    DevelopmentRoundId: round.Id,
                    DevelopmentStepId: step.Id),
                null);

            Assert.False(result.ArtifactPersisted);
            Assert.Null(result.AssociatedRoundId);
            Assert.Null(step.AgentResponseArtifact);
            Assert.False(provider.ExecuteCalled);
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task Handoff_DoesNotPersistWhenPromptChangesDuringExecution()
    {
        var root = CreateTempDirectory();
        try
        {
            var state = CreateWorkspaceState(root);
            var round = Assert.Single(state.ActiveWorkspace!.Iterations);
            var step = new EngineeringIterationStep { Prompt = "Current prompt." };
            round.Steps.Add(step);
            var provider = new FakeProvider
            {
                ExecutionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                ExecutionCompletion = new TaskCompletionSource<CodingAgentProcessResult>(TaskCreationOptions.RunContinuationsAsynchronously)
            };
            var service = new CodingAgentHandoffService(provider, state);
            var handoff = service.SubmitAsync(
                new CodingAgentHandoffRequest(
                    step.Prompt,
                    root,
                    DevelopmentRoundId: round.Id,
                    DevelopmentStepId: step.Id),
                null);

            await provider.ExecutionStarted.Task;
            step.Prompt = "Prompt changed while the agent was running.";
            provider.ExecutionCompletion.SetResult(SuccessResult("response"));
            var result = await handoff;

            Assert.False(result.ArtifactPersisted);
            Assert.Null(result.AssociatedRoundId);
            Assert.Null(step.AgentResponseArtifact);
            Assert.Equal("Prompt changed while the agent was running.", step.Prompt);
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task Handoff_DoesNotPersistIntoCompletedRequestedRound()
    {
        var root = CreateTempDirectory();
        try
        {
            var state = CreateWorkspaceState(root);
            var round = Assert.Single(state.ActiveWorkspace!.Iterations);
            round.Status = IterationStatus.Completed;
            var step = new EngineeringIterationStep { Prompt = "Completed prompt." };
            round.Steps.Add(step);
            var provider = new FakeProvider();
            var service = new CodingAgentHandoffService(provider, state);

            var result = await service.SubmitAsync(
                new CodingAgentHandoffRequest(
                    step.Prompt,
                    root,
                    DevelopmentRoundId: round.Id,
                    DevelopmentStepId: step.Id),
                null);

            Assert.False(result.ArtifactPersisted);
            Assert.Null(result.AssociatedRoundId);
            Assert.Null(step.AgentResponseArtifact);
            Assert.False(provider.ExecuteCalled);
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task Handoff_PersistsFailedCompletionAndExitInformationAsEvidence()
    {
        var root = CreateTempDirectory();
        try
        {
            var state = CreateWorkspaceState(root);
            var provider = new FakeProvider
            {
                Result = new CodingAgentProcessResult(
                    CodingAgentExecutionStatus.Failed,
                    "partial response",
                    "authentication unavailable",
                    7,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    DateTimeOffset.UtcNow,
                    "Copilot authentication unavailable.")
            };
            var service = new CodingAgentHandoffService(provider, state);

            var result = await service.SubmitAsync(new CodingAgentHandoffRequest("prompt", root), null);

            var step = Assert.Single(Assert.Single(state.ActiveWorkspace!.Iterations).Steps);
            Assert.Equal(CodingAgentExecutionStatus.Failed, result.Artifact.Status);
            Assert.Equal(7, result.Artifact.ExitCode);
            Assert.Contains("authentication unavailable", result.Artifact.FailureReason, StringComparison.OrdinalIgnoreCase);
            Assert.Same(result.Artifact, step.AgentResponseArtifact);
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task ProcessRunner_CapturesStdoutStderrAndExitCode()
    {
        var root = CreateTempDirectory();
        try
        {
            var chunks = new List<CodingAgentOutputChunk>();
            var result = await CreateTestProcessRunner().RunAsync(
                new CodingAgentProcessStartInfo(
                    "test-provider",
                    "cmd.exe",
                    new[] { "/c", "echo stdout & echo stderr 1>&2 & exit /b 7" },
                    root,
                    TimeSpan.FromSeconds(10)),
                new Action<CodingAgentOutputChunk>(chunks.Add));

            Assert.Equal(CodingAgentExecutionStatus.Failed, result.Status);
            Assert.Equal(7, result.ExitCode);
            Assert.Contains("stdout", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("stderr", result.StandardError, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(chunks, chunk => !chunk.IsError && chunk.Text.Contains("stdout", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(chunks, chunk => chunk.IsError && chunk.Text.Contains("stderr", StringComparison.OrdinalIgnoreCase));
        }
        finally { DeleteTempDirectory(root); }
    }

    [Fact]
    public async Task ProcessRunner_RejectsExecutableThatIsNotRegisteredForProvider()
    {
        var result = await CreateTestProcessRunner().RunAsync(
            new CodingAgentProcessStartInfo(
                "test-provider",
                "powershell.exe",
                Array.Empty<string>(),
                Environment.CurrentDirectory,
                TimeSpan.FromSeconds(2)),
            null);

        Assert.Equal(CodingAgentExecutionStatus.Failed, result.Status);
        Assert.Contains("not registered", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessRunner_ReportsStartupFailure()
    {
        var result = await CreateTestProcessRunner("copilot-executable-that-does-not-exist-for-test").RunAsync(
            new CodingAgentProcessStartInfo(
                "test-provider",
                "copilot-executable-that-does-not-exist-for-test",
                Array.Empty<string>(),
                Environment.CurrentDirectory,
                TimeSpan.FromSeconds(2)),
            null);

        Assert.Equal(CodingAgentExecutionStatus.Failed, result.Status);
        Assert.NotNull(result.FailureReason);
    }

    [Fact]
    public async Task ProcessRunner_SupportsCancellation()
    {
        using var cancellation = new CancellationTokenSource(150);
        var result = await CreateTestProcessRunner().RunAsync(
            new CodingAgentProcessStartInfo(
                "test-provider",
                "cmd.exe",
                new[] { "/c", "ping 127.0.0.1 -n 30 > nul" },
                Environment.CurrentDirectory,
                TimeSpan.FromSeconds(30)),
            null,
            cancellation.Token);

        Assert.Equal(CodingAgentExecutionStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task ProcessRunner_SupportsAbsoluteTimeoutWithoutDependingOnQuietOutput()
    {
        var result = await CreateTestProcessRunner().RunAsync(
            new CodingAgentProcessStartInfo(
                "test-provider",
                "cmd.exe",
                new[] { "/c", "ping 127.0.0.1 -n 30 > nul" },
                Environment.CurrentDirectory,
                TimeSpan.FromMilliseconds(150)),
            null);

        Assert.Equal(CodingAgentExecutionStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task ProcessRunner_ReportsSuccessfulCompletion()
    {
        var result = await CreateTestProcessRunner().RunAsync(
            new CodingAgentProcessStartInfo(
                "test-provider",
                "cmd.exe",
                new[] { "/c", "echo complete" },
                Environment.CurrentDirectory,
                TimeSpan.FromSeconds(5)),
            null);

        Assert.Equal(CodingAgentExecutionStatus.Succeeded, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("complete", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    private static SystemCodingAgentProcessRunner CreateTestProcessRunner(string executable = "cmd.exe") =>
        new(
            new[] { "test-provider" },
            new Dictionary<string, string> { ["test-provider"] = executable });

    private static WorkspaceState CreateWorkspaceState(string? repositoryPath = null)
    {
        var state = new WorkspaceState(new InMemoryWorkspacePersistence(), new TestRepoFingerprintService());
        state.ReplaceWorkspace(new Workspace
        {
            RepositoryPath = repositoryPath ?? string.Empty,
            Iterations = new List<EngineeringIteration>
            {
                new() { Goal = "Coding-agent handoff" }
            }
        });
        return state;
    }

    private static CodingAgentProcessResult SuccessResult(string output) => new(
        CodingAgentExecutionStatus.Succeeded,
        output,
        string.Empty,
        0,
        DateTimeOffset.UtcNow.AddSeconds(-1),
        DateTimeOffset.UtcNow,
        null);

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "EngineOS-CodingAgent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }

    private sealed class RecordingProcessRunner : ICodingAgentProcessRunner
    {
        public List<CodingAgentProcessStartInfo> StartInfos { get; } = new();
        public CodingAgentProcessResult Result { get; set; } = SuccessResult(string.Empty);

        public Task<CodingAgentProcessResult> RunAsync(
            CodingAgentProcessStartInfo startInfo,
            Action<CodingAgentOutputChunk>? output,
            CancellationToken cancellationToken = default)
        {
            StartInfos.Add(startInfo);
            output?.Invoke(new CodingAgentOutputChunk("streamed", false, DateTimeOffset.UtcNow));
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeProvider : ICodingAgentProvider
    {
        public CodingAgentProviderConfiguration Configuration { get; } = CodingAgentProviderConfiguration.GitHubCopilot();
        public CodingAgentProviderAvailability Availability { get; set; } = new(true, null);
        public CodingAgentProcessResult Result { get; set; } = SuccessResult("agent response");
        public bool ExecuteCalled { get; private set; }
        public TaskCompletionSource<bool>? ExecutionStarted { get; set; }
        public TaskCompletionSource<CodingAgentProcessResult>? ExecutionCompletion { get; set; }

        public Task<CodingAgentProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Availability);

        public Task<CodingAgentProcessResult> ExecuteAsync(
            CodingAgentRequest request,
            Action<CodingAgentOutputChunk>? output,
            CancellationToken cancellationToken = default)
        {
            ExecuteCalled = true;
            ExecutionStarted?.TrySetResult(true);
            return ExecutionCompletion?.Task ?? Task.FromResult(Result);
        }
    }
}
