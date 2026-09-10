using System;

namespace EngineeringDiscovery.Core.Domain.Iteration;

public enum CodingAgentExecutionStatus
{
    Succeeded,
    Failed,
    Cancelled,
    TimedOut
}

/// <summary>
/// Evidence returned by a user-initiated coding-agent handoff.
/// EngineOS records the provider result but does not assess its correctness.
/// </summary>
public sealed class CodingAgentResponseArtifact
{
    public CodingAgentResponseArtifact()
    {
        Id = Guid.NewGuid();
        Provider = string.Empty;
        Prompt = string.Empty;
        Response = string.Empty;
        StandardError = string.Empty;
        StartedUtc = DateTimeOffset.UtcNow;
        CompletedUtc = StartedUtc;
        Status = CodingAgentExecutionStatus.Failed;
    }

    public Guid Id { get; set; }
    public string Provider { get; set; }
    public string Prompt { get; set; }
    public string Response { get; set; }
    public string StandardError { get; set; }
    public int? ExitCode { get; set; }
    public CodingAgentExecutionStatus Status { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset CompletedUtc { get; set; }
    public string? FailureReason { get; set; }
}
