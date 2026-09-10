using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Activity;
using EngineeringDiscovery.Core.Domain.Iteration;
using EngineeringDiscovery.Core.Domain.ProjectState;

namespace EngineeringDiscovery.Core.Services;

/// <summary>
/// Shared, deterministic conversation capability boundary. It handles only
/// explicit, supported requests and returns an auditable response for every
/// handled request. Unknown requests continue through the normal conversation
/// guidance path.
/// </summary>
public sealed class EngineeringConversationCapabilityService : IEngineeringConversationCapabilityService
{
    private readonly IEngineeringStateQuery _stateQuery;
    private readonly IProjectStateService _projectStateService;
    private readonly WorkspaceState _workspaceState;
    private readonly IEngineeringIterationService? _iterationService;
    private readonly IEngineeringOperationGateway? _operationGateway;

    public EngineeringConversationCapabilityService(
        IEngineeringStateQuery stateQuery,
        IProjectStateService projectStateService,
        WorkspaceState workspaceState,
        IEngineeringIterationService? iterationService = null,
        IEngineeringOperationGateway? operationGateway = null)
    {
        _stateQuery = stateQuery ?? throw new ArgumentNullException(nameof(stateQuery));
        _projectStateService = projectStateService ?? throw new ArgumentNullException(nameof(projectStateService));
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        _iterationService = iterationService;
        _operationGateway = operationGateway;
    }

    public EngineeringConversationContext GetContext()
    {
        var workspace = _stateQuery.GetWorkspaceContext();
        var lifecycle = _stateQuery.GetLifecycle();
        var resume = _stateQuery.GetResumePoint();
        var currentHandoff = _stateQuery.GetCurrentHandoff();
        var rounds = GetRounds();
        var currentRound = rounds.LastOrDefault(round => round.Status != IterationStatus.Completed);
        var latestRound = currentRound ?? rounds.LastOrDefault();
        var currentDirection = !string.IsNullOrWhiteSpace(lifecycle?.CurrentFocus)
            ? lifecycle!.CurrentFocus
            : !string.IsNullOrWhiteSpace(resume?.NextRecommendedAction)
                ? resume!.NextRecommendedAction
                : "No current direction established.";

        var openIssues = _stateQuery.GetOpenIssues() ?? Array.Empty<KnownIssue>();
        var engagements = _stateQuery.GetRecentEngagements(20) ?? Array.Empty<WorkerEngagement>();
        var (confidence, attention) = ResolveConfidence(currentDirection, currentHandoff, lifecycle, openIssues, engagements);
        var repository = workspace?.HasRepository == true
            ? string.IsNullOrWhiteSpace(workspace.RepositoryName) ? "Repository loaded." : workspace.RepositoryName
            : "No repository loaded.";
        var investigation = workspace?.HasInvestigation == true
            ? $"{workspace.DiscoveredTypeCount} types, {workspace.DiscoveredNamespaceCount} namespaces, {workspace.DiscoveredMemberCount} members"
            : "Investigation evidence is not available.";

        return new EngineeringConversationContext
        {
            Repository = repository,
            Investigation = investigation,
            CurrentDirection = currentDirection,
            CurrentRound = currentRound is null ? "No active round recorded." : $"Round {rounds.IndexOf(currentRound) + 1}",
            CurrentRoundGoal = currentRound is null
                ? "Development has not started."
                : EmptyFallback(currentRound.Goal, "No round goal recorded."),
            CurrentRoundStatus = currentRound is null ? "No active round recorded." : FormatStatus(currentRound.Status),
            LastCompleted = ResolveLastCompleted(rounds),
            LastCompletedDetails = ResolveLastCompletedDetails(rounds),
            NextHandoff = currentHandoff is null || string.IsNullOrWhiteSpace(currentHandoff.Objective)
                ? "No clear next handoff established."
                : currentHandoff.Objective,
            Confidence = confidence,
            HumanAttention = attention,
            RoundHistory = FormatRoundHistory(rounds),
            Evidence = investigation,
            CurrentRoundSummary = ReadLatest(latestRound, step => step.HumanObservation, "No round summary recorded."),
            CurrentRoundAgentResponse = ReadLatest(latestRound, step => step.CopilotResponse, "No agent response captured."),
            CurrentRoundAssessment = ReadLatest(latestRound, step => step.Assessment, "No EngineOS assessment recorded.")
        };
    }

    public async Task<EngineeringCapabilityResult?> TryHandleAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request)) return null;
        var text = request.Trim();
        var lower = text.ToLowerInvariant();

        if (LooksLikeExternalResearch(lower))
        {
            return new EngineeringCapabilityResult(
                "External research is not connected to this EngineOS session, so no web request was made. I can answer from the loaded repository and recorded engineering evidence, or you can provide a source for us to examine.",
                "External research boundary");
        }

        var direction = ExtractAfter(text, lower,
            "set the current direction to ",
            "change the current direction to ",
            "set current direction to ",
            "change current direction to ",
            "set the current focus to ",
            "set current focus to ",
            "focus the work on ");
        if (direction is not null)
        {
            if (string.IsNullOrWhiteSpace(direction))
            {
                return Clarification("I can change the current direction, but I need the new direction. Nothing was changed.", "Change direction");
            }

            var previous = GetContext().CurrentDirection;
            var phase = _stateQuery.GetLifecycle()?.Phase ?? LifecyclePhase.ActiveDevelopment;
            _projectStateService.UpdateLifecyclePhase(phase, direction);
            return Changed(
                "Current direction updated.",
                "Change direction",
                $"Previous: {previous}\nNew: {direction}");
        }

        var handoff = ExtractAfter(text, lower,
            "set the next handoff to ",
            "change the next handoff to ",
            "set next handoff to ",
            "change next handoff to ",
            "the next handoff is ");
        if (handoff is not null)
        {
            if (string.IsNullOrWhiteSpace(handoff))
            {
                return Clarification("I can update the next handoff, but I need its objective. Nothing was changed.", "Change handoff");
            }

            var previous = GetContext().NextHandoff;
            _projectStateService.AssembleHandoff(new HandoffState { Objective = handoff });
            return Changed(
                "Next handoff updated.",
                "Change handoff",
                $"Previous: {previous}\nNew: {handoff}");
        }

        if (LooksLikeClearHandoff(lower))
        {
            var previous = GetContext().NextHandoff;
            if (previous == "No clear next handoff established.")
            {
                return new EngineeringCapabilityResult("There was no current next handoff to remove. Nothing was changed.", "Clear handoff");
            }

            _projectStateService.ClearCurrentHandoff();
            return Changed("Current next handoff cleared.", "Clear handoff", $"Previous: {previous}\nNew: none");
        }

        if (LooksLikeDecision(lower))
        {
            var statement = ExtractDecisionStatement(text, lower);
            if (string.IsNullOrWhiteSpace(statement))
            {
                return Clarification("I can record a decision, but I need the decision statement. Nothing was changed.", "Record decision");
            }

            _projectStateService.RecordDecision(new EngineeringDecision
            {
                Statement = statement,
                Status = DecisionStatus.Accepted,
                Confidence = 100,
                ResponsibleActorId = "User"
            });
            return Changed("Decision recorded.", "Record decision", $"Statement: {statement}");
        }

        if (LooksLikeCompleteRound(lower))
        {
            if (_iterationService is null)
            {
                return Unavailable("Complete round", "Round management is not connected to this conversation surface. No round was changed.");
            }

            var current = _iterationService.GetCurrentIteration();
            if (current is null)
            {
                return new EngineeringCapabilityResult("There is no active development round to complete. Nothing was changed.", "Complete round");
            }

            var completed = _iterationService.CompleteCurrentIteration();
            return Changed(
                "Development round completed.",
                "Complete round",
                $"Round: {current.Goal}\nStatus: {FormatStatus(completed?.Status ?? IterationStatus.Completed)}");
        }

        if (LooksLikeReassessment(lower))
        {
            return Clarification(
                "I can reconsider the current round assessment, but I need to know what evidence or concern you disagree with. Nothing was changed.",
                "Reassess round");
        }

        if (LooksLikeGeneratePrompt(lower))
        {
            return GeneratePrompt();
        }

        var operation = DetectOperation(lower);
        if (operation.HasValue)
        {
            return await ExecuteOperationAsync(operation.Value, cancellationToken).ConfigureAwait(false);
        }

        if (LooksLikeQuestion(lower))
        {
            return AnswerQuestion(lower);
        }

        return null;
    }

    public async Task<EngineeringCapabilityResult?> TryHandleReadOnlyAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request)) return null;

        var lower = request.Trim().ToLowerInvariant();
        if (LooksLikeExternalResearch(lower))
        {
            return new EngineeringCapabilityResult(
                "External research is not connected to this EngineOS session, so no web request was made. I can answer from the loaded repository and recorded engineering evidence, or you can provide a source for us to examine.",
                "External research boundary");
        }

        if (LooksLikeQuestion(lower))
        {
            return AnswerQuestion(lower);
        }

        return new EngineeringCapabilityResult(
            "This Conversation surface is read-only for now. I can answer from recorded engineering context, but I will not change state or run engineering tools here.",
            "Read-only conversation boundary");
    }

    private EngineeringCapabilityResult GeneratePrompt()
    {
        var handoff = _stateQuery.GetCurrentHandoff();
        if (handoff is null || string.IsNullOrWhiteSpace(handoff.Objective))
        {
            return new EngineeringCapabilityResult(
                "I cannot generate an implementation prompt yet because no clear next handoff is established. Nothing was generated.",
                "Generate prompt");
        }

        var projectState = _projectStateService.GetCurrentState();
        var prompt = EngineeringPromptGenerator.GeneratePrompt(handoff, projectState);
        var rounds = GetRounds();
        var active = rounds.LastOrDefault(round => round.Status != IterationStatus.Completed);
        var persisted = false;
        var roundLabel = "no active round record";

        if (active is not null)
        {
            var step = active.Steps?.LastOrDefault();
            if (step is null || !string.IsNullOrWhiteSpace(step.Prompt) || !string.IsNullOrWhiteSpace(step.CopilotResponse))
            {
                active.Steps ??= new List<EngineeringIterationStep>();
                step = new EngineeringIterationStep();
                active.Steps.Add(step);
            }

            step.Prompt = prompt;
            roundLabel = $"Round {rounds.IndexOf(active) + 1}";
            persisted = _workspaceState.PersistAndNotify();
        }

        var status = persisted
            ? $"Prompt generated and preserved in {roundLabel}."
            : active is null
                ? "Prompt generated, but there is no active round record to preserve it."
                : "Prompt generated in memory, but persistence did not succeed.";
        return new EngineeringCapabilityResult(
            $"{status}\n\n```\n{prompt.TrimEnd()}\n```",
            "Generate prompt",
            changedState: persisted);
    }

    private async Task<EngineeringCapabilityResult> ExecuteOperationAsync(
        EngineeringOperationKind operation,
        CancellationToken cancellationToken)
    {
        var label = FormatOperation(operation);
        if (_operationGateway is null)
        {
            return Unavailable(label, $"The {label.ToLowerInvariant()} capability is not connected to this host. No operation was executed.");
        }

        try
        {
            var result = await _operationGateway.ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
            var details = result.Details.Count == 0
                ? string.Empty
                : "\n" + string.Join("\n", result.Details.Select(detail => $"- {detail}"));
            var status = result.Succeeded ? "succeeded" : "reported a failure";
            return new EngineeringCapabilityResult(
                $"EngineOS action\n\n{label} {status}.\n{result.Summary}{details}",
                label,
                performedOperation: true);
        }
        catch (OperationCanceledException)
        {
            return new EngineeringCapabilityResult($"{label} was cancelled. No successful result was recorded.", label, performedOperation: true);
        }
        catch (Exception ex)
        {
            return new EngineeringCapabilityResult($"I could not complete {label.ToLowerInvariant()}: {ex.Message}\nNo successful result was recorded.", label, performedOperation: true);
        }
    }

    private EngineeringCapabilityResult AnswerQuestion(string lower)
    {
        var context = GetContext();
        if (ContainsAny(lower, "what is this", "what is this project", "what is this repository", "tell me about this project", "tell me about this repository"))
        {
            return new EngineeringCapabilityResult(
                $"Repository: {context.Repository}\nInvestigation: {context.Investigation}\nCurrent direction: {context.CurrentDirection}\nCurrent round: {context.CurrentRound} — {context.CurrentRoundGoal} ({context.CurrentRoundStatus})",
                "Read repository context");
        }

        if (ContainsAny(lower, "where are we", "current status", "project status", "what is the status", "what's the status", "what are we working on", "current direction"))
        {
            return new EngineeringCapabilityResult(
                $"Current direction: {context.CurrentDirection}\nCurrent round: {context.CurrentRound} — {context.CurrentRoundGoal} ({context.CurrentRoundStatus})\nNext handoff: {context.NextHandoff}\nConfidence: {context.Confidence}\nHuman attention: {context.HumanAttention}",
                "Read engineering status");
        }

        if (ContainsAny(lower, "last round", "previous round", "what did we accomplish", "what happened", "what did the last coding-agent round"))
        {
            return new EngineeringCapabilityResult(
                $"Last completed work: {context.LastCompleted}\n\nRound history: {context.RoundHistory}\n\nRecorded last completed round evidence: {context.LastCompletedDetails}",
                "Read round history");
        }

        if (ContainsAny(lower, "confidence", "why is confidence", "why confidence"))
        {
            return new EngineeringCapabilityResult($"Confidence: {context.Confidence}\nHuman attention: {context.HumanAttention}\nNext handoff: {context.NextHandoff}", "Read confidence");
        }

        if (ContainsAny(lower, "handoff unclear", "next handoff", "why do you think the next handoff", "what is the next handoff"))
        {
            return new EngineeringCapabilityResult(
                context.NextHandoff == "No clear next handoff established."
                    ? "The next handoff is unclear because no durable handoff objective is recorded. I will not invent one. Establish a direction and an explicit handoff when you are ready."
                    : $"The recorded next handoff is: {context.NextHandoff}",
                "Read next handoff");
        }

        if (ContainsAny(lower, "what still needs attention", "what needs attention", "human attention", "what remains"))
        {
            return new EngineeringCapabilityResult($"Human attention: {context.HumanAttention}\nOpen engineering attention is reflected in the current confidence and recorded handoff: {context.NextHandoff}", "Read human attention");
        }

        if (ContainsAny(lower, "what evidence", "evidence do we have", "show evidence", "what do we know", "what does engineos know about this repository", "what do you know about this repository", "what is known about this repository"))
        {
            return new EngineeringCapabilityResult($"Repository: {context.Repository}\nInvestigation: {context.Investigation}\nEvidence: {context.Evidence}\n\nChanges, problems, and command results are only reported when a connected operation surface has captured them.", "Read evidence");
        }

        if (ContainsAny(lower, "failures", "failing tests", "test results", "what failed"))
        {
            return new EngineeringCapabilityResult("No durable test failure result is available in the current engineering record. Ask me to run the tests on a host with the safe test capability connected, or inspect the Results and Problems surfaces.", "Read test results");
        }

        return new EngineeringCapabilityResult(
            "I can answer about the current direction, rounds, handoff, confidence, attention, and recorded repository evidence. I can also perform the explicitly supported build, test, evidence, prompt, and state operations when this host provides them.",
            "Read engineering context");
    }

    private static EngineeringOperationKind? DetectOperation(string lower)
    {
        if (ContainsAny(lower, "run the tests", "run tests", "execute tests", "test the application", "test the wpf")) return EngineeringOperationKind.Test;
        if (ContainsAny(lower, "build the wpf", "build the application", "run the build", "run build", "build this application")) return EngineeringOperationKind.Build;
        if (ContainsAny(lower, "refresh repository evidence", "refresh the repository", "refresh evidence", "reload repository evidence")) return EngineeringOperationKind.RefreshEvidence;
        if (ContainsAny(lower, "show current changes", "show me the current changes", "inspect changes", "what changed", "show the changes")) return EngineeringOperationKind.InspectChanges;
        return null;
    }

    private static bool LooksLikeQuestion(string lower) =>
        ContainsAny(lower, "where ", "what ", "what's ", "why ", "how ", "show ", "tell me", "do we ", "is the ") ||
        ContainsAny(lower, "confidence", "handoff", "evidence", "attention", "failures");

    private static bool LooksLikeGeneratePrompt(string lower) =>
        ContainsAny(lower, "generate the implementation prompt", "generate an implementation prompt", "generate the next prompt", "prepare the implementation prompt", "prepare the next prompt");

    private static bool LooksLikeCompleteRound(string lower) =>
        ContainsAny(lower, "mark this round complete", "complete this round", "mark the current round complete", "complete the current round");

    private static bool LooksLikeReassessment(string lower) =>
        ContainsAny(lower, "i don't agree with that assessment", "i disagree with that assessment", "reconsider that assessment", "reassess the current round", "reassess it");

    private static bool LooksLikeClearHandoff(string lower) =>
        ContainsAny(lower, "stop treating that as the next handoff", "clear the next handoff", "remove the next handoff", "there is no next handoff", "no longer the next handoff");

    private static bool LooksLikeDecision(string lower) =>
        ContainsAny(lower, "record that we decided", "record the decision", "record a decision", "we decided not to", "we decided to");

    private static bool LooksLikeExternalResearch(string lower) =>
        ContainsAny(lower, "look up", "search the web", "web search", "current github", "current openai documentation", "is this package version still supported", "external research");

    private static string? ExtractAfter(string original, string lower, params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var index = lower.IndexOf(pattern, StringComparison.Ordinal);
            if (index < 0) continue;
            return TrimStatement(original[(index + pattern.Length)..]);
        }

        return null;
    }

    private static string ExtractDecisionStatement(string original, string lower)
    {
        var statement = ExtractAfter(original, lower, "record that ", "record the decision ", "record a decision ");
        return string.IsNullOrWhiteSpace(statement) ? original : statement;
    }

    private static string TrimStatement(string value) =>
        value.Trim().TrimEnd('.', '!', '?', ':', ';');

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(value.Contains);

    private static EngineeringCapabilityResult Changed(string title, string capability, string details) =>
        new($"EngineOS changed\n\n{title}\n\n{details}", capability, changedState: true);

    private static EngineeringCapabilityResult Clarification(string message, string capability) =>
        new(message, capability, requiresClarification: true);

    private static EngineeringCapabilityResult Unavailable(string capability, string message) =>
        new(message, capability);

    private static string FormatOperation(EngineeringOperationKind operation) => operation switch
    {
        EngineeringOperationKind.Build => "Build",
        EngineeringOperationKind.Test => "Tests",
        EngineeringOperationKind.RefreshEvidence => "Repository evidence refresh",
        EngineeringOperationKind.InspectChanges => "Changes inspection",
        _ => operation.ToString()
    };

    private List<EngineeringIteration> GetRounds() =>
        (_workspaceState.ActiveWorkspace?.Iterations ?? new List<EngineeringIteration>())
            .Where(iteration => iteration is not null)
            .OrderBy(iteration => iteration.CreatedUtc)
            .ToList();

    private static string ResolveLastCompleted(IReadOnlyList<EngineeringIteration> rounds)
    {
        var completed = rounds.LastOrDefault(round => round.Status == IterationStatus.Completed);
        return completed is null
            ? "No completed work recorded."
            : $"Round {rounds.ToList().IndexOf(completed) + 1} — {EmptyFallback(completed.Goal, "Unnamed development round")} · Complete";
    }

    private static string ResolveLastCompletedDetails(IReadOnlyList<EngineeringIteration> rounds)
    {
        var completed = rounds.LastOrDefault(round => round.Status == IterationStatus.Completed);
        if (completed is null) return "No completed round evidence recorded.";

        return $"Summary: {ReadLatest(completed, step => step.HumanObservation, "No round summary recorded.")} " +
               $"Agent response: {ReadLatest(completed, step => step.CopilotResponse, "No agent response captured.")} " +
               $"Assessment: {ReadLatest(completed, step => step.Assessment, "No EngineOS assessment recorded.")}";
    }

    private static string FormatRoundHistory(IReadOnlyList<EngineeringIteration> rounds)
    {
        if (rounds.Count == 0) return "No development rounds recorded.";
        return string.Join("; ", rounds.Select((round, index) =>
            $"Round {index + 1}: {EmptyFallback(round.Goal, "Unnamed development round")} ({FormatStatus(round.Status)}, {round.Steps?.Count ?? 0} step(s))"));
    }

    private static string ReadLatest(EngineeringIteration? round, Func<EngineeringIterationStep, string> selector, string fallback)
    {
        var step = round?.Steps?.LastOrDefault();
        return step is null || string.IsNullOrWhiteSpace(selector(step)) ? fallback : selector(step);
    }

    private static string EmptyFallback(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string FormatStatus(IterationStatus status) => status switch
    {
        IterationStatus.InProgress => "In Progress",
        IterationStatus.Paused => "Paused",
        IterationStatus.Completed => "Complete",
        _ => status.ToString()
    };

    private static (string Confidence, string Attention) ResolveConfidence(
        string direction,
        HandoffState? handoff,
        ProjectLifecycle? lifecycle,
        IReadOnlyList<KnownIssue> issues,
        IReadOnlyList<WorkerEngagement> engagements)
    {
        var critical = issues.FirstOrDefault(issue => issue.Severity == IssueSeverity.Critical);
        var failed = engagements.FirstOrDefault(engagement => engagement.Outcome is EngagementOutcome.Failed or EngagementOutcome.Escalated);
        var partial = engagements.FirstOrDefault(engagement => engagement.Outcome == EngagementOutcome.PartiallyCompleted);
        var rejected = engagements.FirstOrDefault(engagement => engagement.Acceptance == AcceptanceStatus.Rejected);
        var directionMissing = direction == "No current direction established.";
        var handoffMissing = handoff is null || string.IsNullOrWhiteSpace(handoff.Objective);

        if (directionMissing || handoffMissing || critical is not null || failed is not null)
        {
            var confidence = directionMissing || critical is not null || failed is not null
                ? "Low — human direction required"
                : "Low — next handoff is not established";
            var attention = critical is not null
                ? $"Serious problem detected — Critical issue: {critical.Title}"
                : failed is not null
                    ? $"Human decision required — Review {failed.Outcome.ToString().ToLowerInvariant()} work."
                    : directionMissing
                        ? "Direction unclear — establish the current direction."
                        : "Human decision required — establish the next handoff.";
            return (confidence, attention);
        }

        if (lifecycle?.Phase == LifecyclePhase.Paused || partial is not null || rejected is not null)
        {
            return ("Medium — review recommended", lifecycle?.Phase == LifecyclePhase.Paused
                ? "Review recommended — development is paused."
                : partial is not null
                    ? "Review recommended — the latest work is partially complete."
                    : "Review recommended — the latest work was rejected.");
        }

        return ("High — ready to continue", "No attention required");
    }
}
