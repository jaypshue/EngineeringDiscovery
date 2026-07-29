using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    public interface IDiscoveryEngine
    {
        Investigation CreateInvestigation(string? targetOverride = null);
    }
}
