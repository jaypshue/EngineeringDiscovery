using System;

namespace EngineeringDiscovery.Web.Services
{
    internal interface IInvestigationStep
    {
        void Execute(InvestigationContext context);
    }
}
