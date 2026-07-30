using System;

namespace EngineeringDiscovery.Web.Services
{
    internal interface IDiscoveryStep
    {
        void Execute(DiscoveryContext context);
    }
}
