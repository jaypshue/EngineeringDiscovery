using System;
using System.Threading;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services;

public enum ConversationResponseKind
{
    Informational,
    Inspection,
    ProposedAction,
    ConfirmationRequired,
    Unsupported
}

public sealed record StructuredConversationResult(
    ConversationResponseKind Kind,
    string Reply,
    string Capability = "",
    string? ProposedAction = null,
    string? Reason = null,
    EngineeringOperationKind? Operation = null)
{
    public bool RequiresConfirmation => Kind == ConversationResponseKind.ConfirmationRequired;
}

public sealed record ConversationRequestClassification(
    ConversationResponseKind Kind,
    string? ProposedAction = null,
    string? Capability = null,
    string? Reply = null,
    string? Reason = null,
    EngineeringOperationKind? Operation = null);

public static class ConversationRequestClassifier
{
    public static ConversationRequestClassification Classify(string? request)
    {
        var lower = (request ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(lower))
        {
            return new ConversationRequestClassification(ConversationResponseKind.Informational);
        }

        if (ContainsAny(lower, "launch kiro", "start kiro", "launch the ide", "open the ide", "launch an ide", "coding agent", "coding-agent", "run a shell", "run shell", "execute shell", "open a terminal", "use the terminal", "arbitrary command", "search the web", "look up", "web search", "external research"))
        {
            return new ConversationRequestClassification(
                ConversationResponseKind.Unsupported,
                Capability: "Unsupported capability",
                Reply: "That capability is not available through this EngineOS Conversation surface. I will not launch an IDE or coding agent, execute arbitrary shell commands, or make external web requests.",
                Reason: "The requested capability is outside the current EngineOS boundary.");
        }

        if (ContainsAny(lower, "run the tests", "run tests", "execute tests", "test the application", "test the wpf"))
        {
            return Confirmation(
                EngineeringOperationKind.Test,
                "Run the repository test operation",
                "Run tests",
                "This would execute the configured repository test workflow.");
        }

        if (ContainsAny(lower, "build it", "build this", "build the wpf", "build the application", "run the build", "run build", "compile the application"))
        {
            return Confirmation(
                EngineeringOperationKind.Build,
                "Run the repository build operation",
                "Build",
                "This would execute the configured repository build workflow.");
        }

        if (ContainsAny(lower, "change ", "update ", "modify ", "edit ", "fix ", "set the ", "set current", "record ", "mark this round", "complete this round", "generate the implementation prompt", "prepare the implementation prompt", "prepare the next development prompt"))
        {
            return Confirmation("Perform the requested engineering or state-changing action", "State-changing action", "The requested action could change engineering state or produce a persisted artifact.");
        }

        if (ContainsAny(lower, "what would you do", "what would engineos do", "what should you do", "don't do anything", "do not do anything", "just tell me what"))
        {
            return new ConversationRequestClassification(
                ConversationResponseKind.ProposedAction,
                ProposedAction: "Review the current authoritative engineering context and propose the next deliberate step",
                Capability: "Proposed next step",
                Reply: "I would review the current direction, evidence, open attention, and handoff, then propose the next deliberate engineering step. I will not execute it from this request.",
                Reason: "The request asks for a proposal only.");
        }

        if (ContainsAny(lower, "what ", "what's ", "where ", "why ", "how ", "show ", "tell me", "do we ", "is the ", "confidence", "handoff", "evidence", "attention", "failures"))
        {
            return new ConversationRequestClassification(
                ConversationResponseKind.Inspection,
                Capability: "Inspect engineering context",
                Reason: "The request asks about recorded engineering state or evidence.");
        }

        return new ConversationRequestClassification(
            ConversationResponseKind.Informational,
            Capability: "Conversation context",
            Reason: "The request can be answered conversationally without proposing or executing an operation.");
    }

    private static ConversationRequestClassification Confirmation(string action, string capability, string reason) =>
        new(ConversationResponseKind.ConfirmationRequired,
            ProposedAction: action,
            Capability: capability,
            Reply: $"I can propose this action: {action}.\n{reason}\n\nConfirmation required. No operation has been run.",
            Reason: reason);

    private static ConversationRequestClassification Confirmation(
        EngineeringOperationKind operation,
        string action,
        string capability,
        string reason) =>
        new(ConversationResponseKind.ConfirmationRequired,
            ProposedAction: action,
            Capability: capability,
            Reply: $"I can propose this action: {action}.\n{reason}\n\nConfirmation required. No operation has been run.",
            Reason: reason,
            Operation: operation);

    private static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.Contains(fragment, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}

public interface IStructuredConversationPartner
{
    Task<StructuredConversationResult> SendStructuredReadOnlyMessageAsync(
        Guid sessionId,
        string message,
        CancellationToken cancellationToken = default);
}
