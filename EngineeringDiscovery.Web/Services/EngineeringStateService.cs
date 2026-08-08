using System;
using Microsoft.Extensions.Logging;

namespace EngineeringDiscovery.Web.Services
{
    // Lightweight service that derives a single Next Action from the current engineering state.
    // It listens for contract changes and workspace state and publishes a human-friendly next action
    // string into WorkspaceStateService.SetWorkContractSummary so the UI can display guidance.
    public class EngineeringStateService
    {
        private readonly WorkspaceStateService _workspaceState;
        private readonly WorkContractService _contractService;
        private readonly ILogger<EngineeringStateService> _log;

        public EngineeringStateService(WorkspaceStateService workspaceState, WorkContractService contractService, ILogger<EngineeringStateService> log)
        {
            _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
            _contractService = contractService ?? throw new ArgumentNullException(nameof(contractService));
            _log = log;

            // Subscribe to contract changes and compute guidance
            _contractService.ContractChanged += ComputeAndPublish;

            // Recompute when workspace state changes (e.g., repository attached)
            _workspaceState.StateChanged += ComputeAndPublish;

            // Also compute initial guidance
            ComputeAndPublish();
        }

        private void ComputeAndPublish()
        {
            try
            {
                var repoAttached = !string.IsNullOrEmpty(_workspaceState.ActiveRepositoryName);
                var contract = _contractService.CurrentContract;

                string nextAction;
                string why;
                string nextState;

                if (!repoAttached)
                {
                    nextAction = "Attach Repository";
                    why = "No repository is attached; EngineOS needs repository context to analyze the codebase.";
                    nextState = "Repository Attached";
                    PublishSummary(contract, nextAction, why, nextState);
                    return;
                }

                if (contract == null || contract.IsEmpty())
                {
                    nextAction = "Start New Work Contract";
                    why = "No active Work Contract exists for this repository.";
                    nextState = "Draft";
                    PublishSummary(contract, nextAction, why, nextState);
                    return;
                }

                // Contract exists - evaluate status and readiness
                if (string.Equals(contract.Status, "Editing", StringComparison.OrdinalIgnoreCase))
                {
                    nextAction = "Review Draft";
                    why = "The contract is still in editing state; review and refine the draft.";
                    nextState = "Under Review";
                    PublishSummary(contract, nextAction, why, nextState);
                    return;
                }

                if (string.Equals(contract.Status, "Ready", StringComparison.OrdinalIgnoreCase))
                {
                    nextAction = "Submit to Implementation";
                    why = "Both Human and EngineOS indicate readiness; submit the contract to implementation.";
                    nextState = "Implementation Ready";
                    PublishSummary(contract, nextAction, why, nextState);
                    return;
                }

                // Fallback: present a conservative next action based on readiness
                if (contract.HumanReady && !contract.EngineOSReady)
                {
                    nextAction = "Request EngineOS Validation";
                    why = "Human marked ready but EngineOS validation is missing.";
                    nextState = "Under Review";
                }
                else if (!contract.HumanReady && contract.EngineOSReady)
                {
                    nextAction = "Review and Approve";
                    why = "EngineOS marked ready; review and set Human Ready to proceed.";
                    nextState = "Under Review";
                }
                else
                {
                    nextAction = "Review Draft";
                    why = "Review the contract to determine the next steps.";
                    nextState = "Under Review";
                }

                PublishSummary(contract, nextAction, why, nextState);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "EngineeringStateService failed to compute guidance");
            }
        }

        private void PublishSummary(Models.WorkContract? contract, string action, string why, string nextState)
        {
            // Compose a human-friendly recommendation that answers Why / What / Next
            // Compose a short label and a longer detail string answering Why / What / Next
            var label = action;
            var detail = $"Why: {why} What happens if you complete it: {action}. Next state: {nextState}.";

            var title = contract?.Title ?? string.Empty;
            var status = contract?.Status ?? string.Empty;
            var humanReady = contract?.HumanReady ?? false;
            var engineReady = contract?.EngineOSReady ?? false;
            var updated = contract?.UpdatedUtc;
            var lastUpdatedBy = contract?.LastUpdatedBy ?? string.Empty;

            _workspaceState.SetWorkContractSummary(title, status, humanReady, engineReady, updated, lastUpdatedBy, label, detail);
        }
    }
}
