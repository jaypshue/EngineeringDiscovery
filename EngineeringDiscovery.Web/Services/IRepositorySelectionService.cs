using System;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Web.Services
{
    public enum RepoType
    {
        None,
        DotNet,
        JavaMaven,
        JavaGradle
    }

    public interface IRepositorySelectionService
    {
        // Current server-observed or client-provided selected path
        string? SelectedPath { get; }

        RepoType DetectedType { get; }
        string DetectedName { get; }
        int DetectedProjectCount { get; }
        bool ClientSelectionDetected { get; }
        bool IsDetecting { get; }
        bool IsImportEnabled { get; }
        string? ErrorMessage { get; }

        // Called when the user types/pastes a path and requests validation (blur or Enter)
        Task SelectPathAsync(string? path);

        // Called when the client-side picker provides a detection summary
        Task SelectClientSummaryAsync(object summaryObj);

        // Event invoked when presentation state changes; consumers call StateHasChanged
        event Action? StateChanged;
    }
}
