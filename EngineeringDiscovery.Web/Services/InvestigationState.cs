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
            // Diagnostics: log investigation identity being set into state
            try { System.Diagnostics.Debug.WriteLine($"InvestigationState.SetInvestigation: InvHash={(investigation?.GetHashCode().ToString() ?? "null")}, Namespaces={(investigation?.NamespaceObservations?.Count ?? 0)}, Types={(investigation?.TypeObservations?.Count ?? 0)}"); } catch { }
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
