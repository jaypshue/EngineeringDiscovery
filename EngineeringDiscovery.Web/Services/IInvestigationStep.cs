using System;

namespace EngineeringDiscovery.Web.Services
{
    internal interface IInvestigationStep
    {
        InvestigationPhase Phase { get; }

        void Execute(InvestigationContext context);
    }
}
