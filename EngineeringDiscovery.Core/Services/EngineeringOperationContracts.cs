using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services;

/// <summary>
/// Explicit allow-list for operations that a conversation may request.
/// Arbitrary shell commands and coding-agent execution are intentionally absent.
/// </summary>
public enum EngineeringOperationKind
{
    Build,
    Test,
    RefreshEvidence,
    InspectChanges
}

public sealed class EngineeringOperationResult
{
    public EngineeringOperationResult(
        EngineeringOperationKind operation,
        bool succeeded,
        string summary,
        IReadOnlyList<string>? details = null)
    {
        Operation = operation;
        Succeeded = succeeded;
        Summary = summary ?? string.Empty;
        Details = details ?? Array.Empty<string>();
    }

    public EngineeringOperationKind Operation { get; }
    public bool Succeeded { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Details { get; }
}

/// <summary>
/// Host adapter for safe, explicitly allow-listed engineering operations.
/// Hosts that do not have an operation surface leave this capability unregistered;
/// the conversation then reports that nothing was executed.
/// </summary>
public interface IEngineeringOperationGateway
{
    Task<EngineeringOperationResult> ExecuteAsync(
        EngineeringOperationKind operation,
        CancellationToken cancellationToken = default);
}
