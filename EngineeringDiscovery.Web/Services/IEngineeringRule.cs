using System.Collections.Generic;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal interface IEngineeringRule
    {
        IEnumerable<InvestigationArtifact> Evaluate(Investigation investigation, string sourceLayer, string referencedLayer, string relationshipDescription);
    }
}
