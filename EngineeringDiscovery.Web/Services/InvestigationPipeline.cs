using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Services
{
    internal class InvestigationPipeline
    {
        private readonly List<IInvestigationStep> _steps = new();

        public InvestigationPipeline Add(IInvestigationStep step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            _steps.Add(step);
            return this;
        }

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;
            foreach (var s in _steps)
            {
                try
                {
                    s.Execute(context);
                }
                catch(Exception ex) 
                {
                    context.AddDiagnostic(ex.ToString());
                }
            }
        }
    }
}
