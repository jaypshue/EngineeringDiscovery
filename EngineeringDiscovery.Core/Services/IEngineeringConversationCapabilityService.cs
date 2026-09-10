using System.Threading;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services;

public interface IEngineeringConversationCapabilityService
{
    EngineeringConversationContext GetContext();
    Task<EngineeringCapabilityResult?> TryHandleAsync(string request, CancellationToken cancellationToken = default);
    Task<EngineeringCapabilityResult?> TryHandleReadOnlyAsync(string request, CancellationToken cancellationToken = default);
}

public sealed class EngineeringCapabilityResult
{
    public EngineeringCapabilityResult(
        string reply,
        string capability,
        bool changedState = false,
        bool performedOperation = false,
        bool requiresClarification = false)
    {
        Reply = reply ?? string.Empty;
        Capability = capability ?? string.Empty;
        ChangedState = changedState;
        PerformedOperation = performedOperation;
        RequiresClarification = requiresClarification;
    }

    public string Reply { get; }
    public string Capability { get; }
    public bool ChangedState { get; }
    public bool PerformedOperation { get; }
    public bool RequiresClarification { get; }
}

/// <summary>
/// User-facing projection of known engineering context. It deliberately uses
/// software/repository/round language rather than exposing the internal Project model.
/// </summary>
public sealed class EngineeringConversationContext
{
    public string Repository { get; init; } = "No repository loaded.";
    public string Investigation { get; init; } = "Investigation evidence is not available.";
    public string CurrentDirection { get; init; } = "No current direction established.";
    public string CurrentRound { get; init; } = "No active round recorded.";
    public string CurrentRoundGoal { get; init; } = "Development has not started.";
    public string CurrentRoundStatus { get; init; } = "No active round recorded.";
    public string LastCompleted { get; init; } = "No completed work recorded.";
    public string LastCompletedDetails { get; init; } = "No completed round evidence recorded.";
    public string NextHandoff { get; init; } = "No clear next handoff established.";
    public string Confidence { get; init; } = "Low — human direction required.";
    public string HumanAttention { get; init; } = "Human attention required.";
    public string RoundHistory { get; init; } = "No development rounds recorded.";
    public string Evidence { get; init; } = "No investigation evidence is available.";
    public string CurrentRoundSummary { get; init; } = "No round summary recorded.";
    public string CurrentRoundAgentResponse { get; init; } = "No agent response captured.";
    public string CurrentRoundAssessment { get; init; } = "No EngineOS assessment recorded.";

    public string ToPromptText()
    {
        return $"Repository: {Repository}\n" +
               $"Investigation: {Investigation}\n" +
               $"Current direction: {CurrentDirection}\n" +
               $"Current round: {CurrentRound} — {CurrentRoundGoal} ({CurrentRoundStatus})\n" +
               $"Last completed: {LastCompleted}\n" +
               $"Last completed round evidence: {LastCompletedDetails}\n" +
               $"Next handoff: {NextHandoff}\n" +
               $"Confidence: {Confidence}\n" +
               $"Human attention: {HumanAttention}\n" +
               $"Round history: {RoundHistory}\n" +
               $"Evidence: {Evidence}\n" +
               $"Current round summary: {CurrentRoundSummary}\n" +
               $"Current round agent response: {CurrentRoundAgentResponse}\n" +
               $"Current round assessment: {CurrentRoundAssessment}";
    }
}
