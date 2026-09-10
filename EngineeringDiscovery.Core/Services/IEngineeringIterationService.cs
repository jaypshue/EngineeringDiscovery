using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Iteration;

namespace EngineeringDiscovery.Core.Services;

/// <summary>
/// Shared application boundary for the engineering-round records currently stored
/// on Workspace. It uses the canonical WorkspaceState persistence/notification path.
/// </summary>
public interface IEngineeringIterationService
{
    EngineeringIteration? GetCurrentIteration();
    IReadOnlyList<EngineeringIteration> GetHistory();
    EngineeringIteration StartIteration(string goal);
    void AddStep(EngineeringIteration iteration, EngineeringIterationStep step);
    void CompleteIteration(EngineeringIteration iteration);
    EngineeringIteration? CompleteCurrentIteration();
}
