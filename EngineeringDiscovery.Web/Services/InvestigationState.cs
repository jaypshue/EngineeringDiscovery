using System;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Web.Services;

namespace EngineeringDiscovery.Web.Services
{
    public class InvestigationState
    {
        private readonly EngineeringDiscovery.Core.Services.IViewStateStore _viewStateStore;

        public InvestigationState(EngineeringDiscovery.Core.Services.IViewStateStore viewStateStore)
        {
            _viewStateStore = viewStateStore;
        }

        public Investigation? Investigation { get; private set; }

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

        // Adapter over IViewStateStore — presentation-only storage.
        public GraphViewState? GetGraphViewState(string key)
        {
            return _viewStateStore.Get(key) as GraphViewState;
        }

        public void SetGraphViewState(string key, GraphViewState? state)
        {
            _viewStateStore.Set(key, state);
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
