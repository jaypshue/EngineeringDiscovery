using System;
using EngineeringDiscovery.Web.Models;

namespace EngineeringDiscovery.Web.Services
{
    // Session-scoped service that manages a single in-memory Work Contract for ED-303
    public class WorkContractService
    {
        private readonly WorkspaceStateService _workspaceState;

        public WorkContractService(WorkspaceStateService workspaceState)
        {
            _workspaceState = workspaceState;
        }

        private WorkContract? _current;

        public WorkContract? CurrentContract => _current;

        public event Action? ContractChanged;

        public WorkContract CreateDraft(WorkContract template)
        {
            _current = template ?? new WorkContract();
            _current.UpdatedUtc = DateTime.UtcNow;
            _current.Status = "Editing";
            _current.HumanReady = false;
            _current.EngineOSReady = false;
            _current.LastUpdatedBy = "You";
            // Update presentation summary
            PushSummaryToWorkspace();
            ContractChanged?.Invoke();
            return _current;
        }

        public void UpdateField(Action<WorkContract> updater)
        {
            if (_current == null) return;
            updater(_current);
            // Any edit resets readiness
            _current.HumanReady = false;
            _current.EngineOSReady = false;
            _current.Status = "Editing";
            _current.UpdatedUtc = DateTime.UtcNow;
            _current.LastUpdatedBy = "You";
            PushSummaryToWorkspace();
            ContractChanged?.Invoke();
        }

        public void SetHumanReady(bool ready)
        {
            if (_current == null) return;
            _current.HumanReady = ready;
            EvaluateStatus();
            _current.LastUpdatedBy = "You";
            PushSummaryToWorkspace();
            ContractChanged?.Invoke();
        }

        public void SetEngineOSReady(bool ready)
        {
            if (_current == null) return;
            _current.EngineOSReady = ready;
            EvaluateStatus();
            _current.LastUpdatedBy = "EngineOS";
            PushSummaryToWorkspace();
            ContractChanged?.Invoke();
        }

        // Apply a proposed change to the contract originating from EngineOS (or other author).
        // The updater should modify the WorkContract fields as needed. The author indicates who
        // is credited with the change (e.g., "EngineOS"). Applying a proposal clears readiness
        // and sets Status back to Editing.
        public void ApplyProposal(Action<WorkContract> updater, string author = "EngineOS")
        {
            if (_current == null) return;
            updater(_current);
            // Any accepted proposal resets readiness and returns to Editing
            _current.HumanReady = false;
            _current.EngineOSReady = false;
            _current.Status = "Editing";
            _current.UpdatedUtc = DateTime.UtcNow;
            _current.LastUpdatedBy = author ?? string.Empty;
            PushSummaryToWorkspace();
            ContractChanged?.Invoke();
        }

        private void EvaluateStatus()
        {
            if (_current == null) return;
            _current.Status = (_current.HumanReady && _current.EngineOSReady) ? "Ready" : "Editing";
            _current.UpdatedUtc = DateTime.UtcNow;
        }

        private void PushSummaryToWorkspace()
        {
            if (_current == null) return;
            _workspaceState.SetWorkContractSummary(
                _current.Title,
                _current.Status,
                _current.HumanReady,
                _current.EngineOSReady,
                _current.UpdatedUtc,
                _current.LastUpdatedBy);
        }

        public WorkContract CreateEmptyDraft()
        {
            var c = new WorkContract();
            c.UpdatedUtc = DateTime.UtcNow;
            c.Status = "Editing";
            c.HumanReady = false;
            c.EngineOSReady = false;
            c.LastUpdatedBy = string.Empty;
            _current = c;
            PushSummaryToWorkspace();
            ContractChanged?.Invoke();
            return _current;
        }
    }
}
