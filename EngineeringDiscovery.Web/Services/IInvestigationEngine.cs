using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    public interface IInvestigationEngine
    {
        Investigation CreateInvestigation(string? repositoryRoot = null, string? targetOverride = null);
    }
}
