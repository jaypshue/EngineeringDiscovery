using System;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal class ObservationEnrichmentStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Analysis; // enrichment occurs before analysis

        private readonly Investigation _inv;

        public ObservationEnrichmentStep(Investigation inv)
        {
            _inv = inv ?? throw new ArgumentNullException(nameof(inv));
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            try
            {
                foreach (var p in ObservationEnrichmentPipeline.Passes())
                {
                    try { p.Enrich(_inv); } catch { }
                }
            }
            catch { }
        }
    }
}
