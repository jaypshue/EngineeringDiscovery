using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    public interface IDiscoveryEngine
    {
        Investigation CreateInvestigation(string? repositoryRoot = null, string? targetOverride = null);
    }
}
