using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace EngineeringDiscovery.Web.Services
{
    // Lightweight facade to expose workspace state to UI components.
    // This class adapts whatever startup/session service exists in the app and surfaces
    // a small set of properties and a StateChanged event. It intentionally avoids
    // introducing new persistence or storage responsibilities.
    public class WorkspaceStateService
    {
        private readonly ILogger<WorkspaceStateService> _log;

        public WorkspaceStateService(ILogger<WorkspaceStateService> log)
        {
            _log = log;
        }

        // Public state properties updated by the startup service (or other orchestrators)
        public string ActiveRepositoryName { get; private set; } = string.Empty;
        public string ActiveRepositoryPath { get; private set; } = string.Empty;
        public string CurrentGoalTitle { get; private set; } = string.Empty;
        public string CurrentStoryTitle { get; private set; } = string.Empty;
        public string Status { get; private set; } = "Ready";

        public event Action? StateChanged;

        public void SetState(string repoName, string repoPath, string goalTitle, string storyTitle, string status)
        {
            ActiveRepositoryName = repoName ?? string.Empty;
            ActiveRepositoryPath = repoPath ?? string.Empty;
            CurrentGoalTitle = goalTitle ?? string.Empty;
            CurrentStoryTitle = storyTitle ?? string.Empty;
            Status = status ?? string.Empty;
            try
            {
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "WorkspaceStateService.StateChanged handler threw");
            }
        }

        // Work Contract summary (session-scoped view)
        public string ActiveWorkContractTitle { get; private set; } = string.Empty;
        public string ActiveWorkContractStatus { get; private set; } = string.Empty;
        public bool ActiveWorkContractHumanReady { get; private set; }
        public bool ActiveWorkContractEngineOSReady { get; private set; }
        public DateTime? ActiveWorkContractUpdatedUtc { get; private set; }

        public void SetWorkContractSummary(string title, string status, bool humanReady, bool engineReady, DateTime? updatedUtc)
        {
            ActiveWorkContractTitle = title ?? string.Empty;
            ActiveWorkContractStatus = status ?? string.Empty;
            ActiveWorkContractHumanReady = humanReady;
            ActiveWorkContractEngineOSReady = engineReady;
            ActiveWorkContractUpdatedUtc = updatedUtc;
            try
            {
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "WorkspaceStateService.StateChanged handler threw");
            }
        }
    }
}
