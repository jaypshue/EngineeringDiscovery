using System.Collections.Generic;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal interface IEngineeringRule
    {
        // Evaluate against investigation and optional dependency adjacency information.
        // Implementations may ignore the adjacency parameter if not needed.
        IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, IDictionary<string, List<string>>? adjacency = null, string? sourceLayer = null, string? referencedLayer = null, string? relationshipDescription = null);
    }
}
