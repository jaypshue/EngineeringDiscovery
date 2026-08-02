using System;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    public class InvestigationState
    {
        public Investigation? Investigation { get; private set; }

        // Persisted view state for the graph workspace
        public GraphViewState? GraphViewState { get; set; }

        public event Action? OnChange;

        public void SetInvestigation(Investigation? investigation)
        {
            Investigation = investigation;
            NotifyStateChanged();
        }

        public void Clear()
        {
            Investigation = null;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
