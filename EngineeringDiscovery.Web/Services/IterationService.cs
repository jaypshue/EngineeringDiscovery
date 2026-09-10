using System;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Iteration;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Web.Services;

// Presentation adapter for the shared engineering-round capability boundary.
public sealed class IterationService
{
    private readonly IEngineeringIterationService _iterations;

    public IterationService(IEngineeringIterationService iterations)
    {
        _iterations = iterations ?? throw new ArgumentNullException(nameof(iterations));
    }

    public EngineeringIteration? GetCurrentIteration() => _iterations.GetCurrentIteration();

    public EngineeringIteration StartIteration(string goal) => _iterations.StartIteration(goal);

    public void AddStep(EngineeringIteration iteration, EngineeringIterationStep step) => _iterations.AddStep(iteration, step);

    public void CompleteIteration(EngineeringIteration iteration) => _iterations.CompleteIteration(iteration);

    public IReadOnlyList<EngineeringIteration> GetHistory() => _iterations.GetHistory();
}
