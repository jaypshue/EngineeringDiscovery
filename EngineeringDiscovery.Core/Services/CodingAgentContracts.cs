using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Iteration;

namespace EngineeringDiscovery.Core.Services;

public sealed record CodingAgentProviderConfiguration(
    string ProviderId,
    string DisplayName,
    string Executable,
    TimeSpan DefaultTimeout,
    IReadOnlyList<string> AllowedTools)
{
    public static CodingAgentProviderConfiguration GitHubCopilot(string executable = "copilot") =>
        new(
            "github-copilot-cli",
            "GitHub Copilot CLI",
            executable,
            TimeSpan.FromMinutes(15),
            new[] { "write" });
}

public sealed record CodingAgentRequest(
    string Prompt,
    string WorkingDirectory,
    TimeSpan? Timeout = null);

public sealed record CodingAgentOutputChunk(
    string Text,
    bool IsError,
    DateTimeOffset Timestamp);

public sealed record CodingAgentProcessStartInfo(
    string ProviderId,
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    TimeSpan Timeout);

public sealed record CodingAgentProcessResult(
    CodingAgentExecutionStatus Status,
    string StandardOutput,
    string StandardError,
    int? ExitCode,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    string? FailureReason);

public sealed record CodingAgentProviderAvailability(
    bool IsAvailable,
    string? FailureReason,
    CodingAgentExecutionStatus? Status = null,
    string StandardError = "");

public interface ICodingAgentProcessRunner
{
    Task<CodingAgentProcessResult> RunAsync(
        CodingAgentProcessStartInfo startInfo,
        Action<CodingAgentOutputChunk>? output,
        CancellationToken cancellationToken = default);
}

public interface ICodingAgentProvider
{
    CodingAgentProviderConfiguration Configuration { get; }

    Task<CodingAgentProviderAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default);

    Task<CodingAgentProcessResult> ExecuteAsync(
        CodingAgentRequest request,
        Action<CodingAgentOutputChunk>? output,
        CancellationToken cancellationToken = default);
}

public sealed record CodingAgentHandoffRequest(
    string Prompt,
    string RepositoryPath,
    TimeSpan? Timeout = null,
    Guid? DevelopmentRoundId = null,
    Guid? DevelopmentStepId = null);

public sealed record CodingAgentHandoffResult(
    CodingAgentResponseArtifact Artifact,
    Guid? AssociatedRoundId,
    Guid? AssociatedStepId,
    bool ArtifactPersisted,
    string? PersistenceFailureReason)
{
    public bool Succeeded => Artifact.Status == EngineeringDiscovery.Core.Domain.Iteration.CodingAgentExecutionStatus.Succeeded;
}

public interface ICodingAgentHandoffService
{
    Task<CodingAgentHandoffResult> SubmitAsync(
        CodingAgentHandoffRequest request,
        Action<CodingAgentOutputChunk>? output,
        CancellationToken cancellationToken = default);
}
