using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Iteration;

namespace EngineeringDiscovery.Core.Services;

/// <summary>
/// Shared round-state boundary used by normal controls and conversation.
/// </summary>
public sealed class EngineeringIterationService : IEngineeringIterationService
{
    private readonly WorkspaceState _workspaceState;

    public EngineeringIterationService(WorkspaceState workspaceState)
    {
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
    }

    public EngineeringIteration StartIteration(string goal)
    {
        var workspace = _workspaceState.ActiveWorkspace;
        if (workspace is null)
        {
            workspace = new EngineeringDiscovery.Core.Domain.Workspace.Workspace();
            _workspaceState.ReplaceWorkspace(workspace);
        }

        workspace.Iterations ??= new List<EngineeringIteration>();
        var iteration = new EngineeringIteration { Goal = goal ?? string.Empty };
        workspace.Iterations.Add(iteration);
        _workspaceState.PersistAndNotify();
        return iteration;
    }

    public void AddStep(EngineeringIteration iteration, EngineeringIterationStep step)
    {
        if (iteration is null) throw new ArgumentNullException(nameof(iteration));
        if (step is null) throw new ArgumentNullException(nameof(step));

        iteration.Steps ??= new List<EngineeringIterationStep>();
        iteration.Steps.Add(step);
        _workspaceState.PersistAndNotify();
    }

    public void CompleteIteration(EngineeringIteration iteration)
    {
        if (iteration is null) return;
        iteration.Status = IterationStatus.Completed;
        _workspaceState.PersistAndNotify();
    }

    public EngineeringIteration? GetCurrentIteration() =>
        _workspaceState.ActiveWorkspace?.Iterations?
            .Where(iteration => iteration is not null && iteration.Status != IterationStatus.Completed)
            .OrderBy(iteration => iteration.CreatedUtc)
            .LastOrDefault();

    public IReadOnlyList<EngineeringIteration> GetHistory() =>
        (_workspaceState.ActiveWorkspace?.Iterations ?? new List<EngineeringIteration>())
            .Where(iteration => iteration is not null)
            .OrderBy(iteration => iteration.CreatedUtc)
            .ToList()
            .AsReadOnly();

    public EngineeringIteration? CompleteCurrentIteration()
    {
        var current = GetCurrentIteration();
        if (current is null) return null;

        current.Status = IterationStatus.Completed;
        _workspaceState.PersistAndNotify();
        return current;
    }
}
