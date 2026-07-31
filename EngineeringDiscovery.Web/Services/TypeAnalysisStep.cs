using System;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    internal class TypeAnalysisStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Analysis;
        private readonly Investigation _inv;

        public TypeAnalysisStep(Investigation inv)
        {
            _inv = inv ?? throw new ArgumentNullException(nameof(inv));
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            try
            {
                foreach (var r in EngineeringRuleCatalog.TypeRules())
                {
                    try
                    {
                        var artifacts = r.Evaluate(_inv);
                        foreach (var a in artifacts) _inv.Artifacts.Add(a);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
