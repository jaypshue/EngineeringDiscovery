using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Iteration;

namespace EngineeringDiscovery.Core.Services;

/// <summary>
/// Coordinates one explicit provider handoff and records its response as round evidence.
/// This service does not assess, approve, or continue the work.
/// </summary>
public sealed class CodingAgentHandoffService : ICodingAgentHandoffService
{
    private readonly ICodingAgentProvider _provider;
    private readonly WorkspaceState _workspaceState;

    public CodingAgentHandoffService(ICodingAgentProvider provider, WorkspaceState workspaceState)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
    }

    public async Task<CodingAgentHandoffResult> SubmitAsync(
        CodingAgentHandoffRequest request,
        Action<CodingAgentOutputChunk>? output,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var startedUtc = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(request.Prompt))
            return CompleteFailure(request, startedUtc, "A generated engineering prompt is required.");
        if (string.IsNullOrWhiteSpace(request.RepositoryPath) || !Directory.Exists(request.RepositoryPath))
            return CompleteFailure(request, startedUtc, "The selected repository/workspace does not exist.");

        var workspace = _workspaceState.ActiveWorkspace;
        if (!string.IsNullOrWhiteSpace(workspace?.RepositoryPath) &&
            !PathsReferToSameDirectory(request.RepositoryPath, workspace.RepositoryPath))
        {
            return CompleteFailureWithoutPersistence(
                request,
                startedUtc,
                "The selected repository does not match the active workspace repository.");
        }

        if (request.DevelopmentRoundId is Guid requestedRoundId)
        {
            var requestedRound = workspace?.Iterations?.FirstOrDefault(round => round.Id == requestedRoundId);
            if (requestedRound is null || requestedRound.Status == IterationStatus.Completed)
                return CompleteFailureWithoutPersistence(request, startedUtc, "The development round for this prompt is no longer active.");

            if (request.DevelopmentStepId is Guid requestedStepId)
            {
                var requestedStep = requestedRound.Steps?.FirstOrDefault(step => step.Id == requestedStepId);
                if (requestedStep is null || !string.Equals(requestedStep.Prompt, request.Prompt, StringComparison.Ordinal))
                    return CompleteFailureWithoutPersistence(request, startedUtc, "The development step for this prompt no longer matches the generated prompt.");
            }
        }
        else if (request.DevelopmentStepId is not null)
        {
            return CompleteFailureWithoutPersistence(request, startedUtc, "A development step association requires a development round association.");
        }

        CodingAgentProviderAvailability availability;
        try
        {
            availability = await _provider.CheckAvailabilityAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CompleteCancelled(request, startedUtc, "Coding-agent availability check was cancelled.");
        }
        catch (Exception ex)
        {
            return CompleteFailure(request, startedUtc, $"Coding-agent provider check failed: {ex.Message}");
        }

        if (!availability.IsAvailable)
        {
            if (availability.Status == CodingAgentExecutionStatus.Cancelled)
            {
                return CompleteCancelled(request, startedUtc, "Coding-agent availability check was cancelled.");
            }

            return CompleteFailure(
                request,
                startedUtc,
                string.IsNullOrWhiteSpace(availability.FailureReason)
                    ? "The configured coding-agent provider is unavailable."
                    : availability.FailureReason,
                availability.StandardError);
        }

        CodingAgentProcessResult processResult;
        try
        {
            processResult = await _provider.ExecuteAsync(
                new CodingAgentRequest(request.Prompt, request.RepositoryPath, request.Timeout),
                output,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CompleteCancelled(request, startedUtc, "Coding-agent handoff was cancelled by the user.");
        }
        catch (Exception ex)
        {
            return CompleteFailure(request, startedUtc, $"Coding-agent process failed to start: {ex.Message}");
        }

        var artifact = CreateArtifact(request.Prompt, processResult);
        return PersistArtifact(request, artifact);
    }

    private CodingAgentHandoffResult CompleteFailureWithoutPersistence(
        CodingAgentHandoffRequest request,
        DateTimeOffset startedUtc,
        string reason,
        string standardError = "")
    {
        var artifact = new CodingAgentResponseArtifact
        {
            Provider = _provider.Configuration.ProviderId,
            Prompt = request.Prompt ?? string.Empty,
            Response = string.Empty,
            StandardError = standardError,
            Status = CodingAgentExecutionStatus.Failed,
            StartedUtc = startedUtc,
            CompletedUtc = DateTimeOffset.UtcNow,
            FailureReason = reason
        };
        return new CodingAgentHandoffResult(
            artifact,
            null,
            null,
            false,
            reason);
    }

    private CodingAgentHandoffResult CompleteFailure(
        CodingAgentHandoffRequest request,
        DateTimeOffset startedUtc,
        string reason,
        string standardError = "")
    {
        var artifact = new CodingAgentResponseArtifact
        {
            Provider = _provider.Configuration.ProviderId,
            Prompt = request.Prompt ?? string.Empty,
            Response = string.Empty,
            StandardError = standardError,
            Status = CodingAgentExecutionStatus.Failed,
            StartedUtc = startedUtc,
            CompletedUtc = DateTimeOffset.UtcNow,
            FailureReason = reason
        };
        return PersistArtifact(request, artifact);
    }

    private CodingAgentHandoffResult CompleteCancelled(
        CodingAgentHandoffRequest request,
        DateTimeOffset startedUtc,
        string reason)
    {
        var artifact = new CodingAgentResponseArtifact
        {
            Provider = _provider.Configuration.ProviderId,
            Prompt = request.Prompt ?? string.Empty,
            Response = string.Empty,
            Status = CodingAgentExecutionStatus.Cancelled,
            StartedUtc = startedUtc,
            CompletedUtc = DateTimeOffset.UtcNow,
            FailureReason = reason
        };
        return PersistArtifact(request, artifact);
    }

    private CodingAgentResponseArtifact CreateArtifact(
        string prompt,
        CodingAgentProcessResult result) =>
        new()
        {
            Provider = _provider.Configuration.ProviderId,
            Prompt = prompt,
            Response = result.StandardOutput,
            StandardError = result.StandardError,
            ExitCode = result.ExitCode,
            Status = result.Status,
            StartedUtc = result.StartedUtc,
            CompletedUtc = result.CompletedUtc,
            FailureReason = result.Status == CodingAgentExecutionStatus.Succeeded
                ? null
                : string.IsNullOrWhiteSpace(result.FailureReason)
                    ? result.StandardError.Trim()
                    : result.FailureReason
        };

    private static bool PathsReferToSameDirectory(string left, string right)
    {
        try
        {
            var normalizedLeft = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
            var normalizedRight = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private CodingAgentHandoffResult PersistArtifact(
        CodingAgentHandoffRequest request,
        CodingAgentResponseArtifact artifact)
    {
        var workspace = _workspaceState.ActiveWorkspace;
        if (workspace?.Iterations is null || workspace.Iterations.Count == 0)
        {
            return new CodingAgentHandoffResult(
                artifact,
                null,
                null,
                false,
                "No active development round exists to associate this agent response with.");
        }

        var orderedRounds = workspace.Iterations
            .Where(round => round is not null)
            .OrderBy(round => round.CreatedUtc)
            .ToList();
        var round = request.DevelopmentRoundId is Guid requestedRoundId
            ? orderedRounds.FirstOrDefault(candidate => candidate.Id == requestedRoundId)
            : orderedRounds.LastOrDefault(candidate => candidate.Status != IterationStatus.Completed);
        if (round is null || round.Status == IterationStatus.Completed)
        {
            return new CodingAgentHandoffResult(
                artifact,
                null,
                null,
                false,
                "No active development round exists to associate this agent response with.");
        }

        round.Steps ??= new();
        EngineeringIterationStep? step;
        if (request.DevelopmentStepId is Guid requestedStepId)
        {
            step = round.Steps.FirstOrDefault(candidate => candidate.Id == requestedStepId);
            if (step is null || !string.Equals(step.Prompt, request.Prompt, StringComparison.Ordinal))
            {
                return new CodingAgentHandoffResult(
                    artifact,
                    null,
                    null,
                    false,
                    "The development step for this prompt no longer matches the generated prompt.");
            }
        }
        else
        {
            step = round.Steps.LastOrDefault(candidate =>
                string.Equals(candidate.Prompt, request.Prompt, StringComparison.Ordinal) &&
                candidate.AgentResponseArtifact is null &&
                string.IsNullOrWhiteSpace(candidate.CopilotResponse));
        }

        if (step is null)
        {
            step = new EngineeringIterationStep { Prompt = request.Prompt };
            round.Steps.Add(step);
        }

        step.Prompt = request.Prompt;
        step.AgentResponseArtifact = artifact;
        // Preserve the legacy field for existing round projections and persisted data readers.
        step.CopilotResponse = artifact.Response;
        var persisted = _workspaceState.PersistAndNotify();
        return new CodingAgentHandoffResult(
            artifact,
            round.Id,
            step.Id,
            persisted,
            persisted ? null : "The agent response was captured but could not be persisted.");
    }
}
