using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Services;

namespace EngineeringDiscovery.Wpf.ViewModels;

/// <summary>
/// Read-only desktop projection of authoritative project activity and repository state.
/// </summary>
public sealed class EngineeringStatePanelViewModel : INotifyPropertyChanged
{
    private readonly IEngineeringStateQuery _stateQuery;
    private readonly IGitStatusService _gitStatusService;
    private readonly WorkspaceState _workspaceState;
    private int _refreshVersion;
    private string _projectName = "No project";
    private string _lifecycle = "Not established";
    private string _resumeSummary = "No resume point recorded.";
    private string _nextAction = "—";
    private int _openIssueCount;
    private GitStatusSnapshot _gitStatus = GitStatusSnapshot.Empty();

    public EngineeringStatePanelViewModel(
        IEngineeringStateQuery stateQuery,
        IGitStatusService gitStatusService,
        WorkspaceState workspaceState)
    {
        _stateQuery = stateQuery ?? throw new ArgumentNullException(nameof(stateQuery));
        _gitStatusService = gitStatusService ?? throw new ArgumentNullException(nameof(gitStatusService));
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        _workspaceState.OnChange += WorkspaceState_OnChange;
        RecentWork = new ObservableCollection<RecentWorkItemViewModel>();

        RefreshStateProjection();
        _ = RefreshGitStatusAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RecentWorkItemViewModel> RecentWork { get; }

    public string ProjectName
    {
        get => _projectName;
        private set
        {
            if (Equals(_projectName, value)) return;
            _projectName = value;
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectNameDisplay));
        }
    }

    public string ProjectNameDisplay => ProjectName.ToUpperInvariant();

    public string Lifecycle
    {
        get => _lifecycle;
        private set => SetProperty(ref _lifecycle, value, nameof(Lifecycle));
    }

    public string ResumeSummary
    {
        get => _resumeSummary;
        private set => SetProperty(ref _resumeSummary, value, nameof(ResumeSummary));
    }

    public string NextAction
    {
        get => _nextAction;
        private set => SetProperty(ref _nextAction, value, nameof(NextAction));
    }

    public int OpenIssueCount
    {
        get => _openIssueCount;
        private set => SetProperty(ref _openIssueCount, value, nameof(OpenIssueCount));
    }

    public GitStatusSnapshot GitStatus
    {
        get => _gitStatus;
        private set => SetProperty(ref _gitStatus, value, nameof(GitStatus));
    }

    /// <summary>
    /// Refreshes the synchronous state projection and the asynchronous Git projection.
    /// Public visibility keeps the projection directly testable without a WPF window.
    /// </summary>
    public async Task RefreshAsync()
    {
        RefreshStateProjection();
        await RefreshGitStatusAsync();
    }

    private void RefreshStateProjection()
    {
        var identity = _stateQuery.GetProjectIdentity();
        var lifecycle = _stateQuery.GetLifecycle();
        var resumePoint = _stateQuery.GetResumePoint();

        ProjectName = string.IsNullOrWhiteSpace(identity?.Name) ? "No project" : identity.Name;
        Lifecycle = lifecycle is null
            ? "Not established"
            : string.IsNullOrWhiteSpace(lifecycle.CurrentFocus)
                ? lifecycle.Phase.ToString()
                : $"{lifecycle.Phase} · {lifecycle.CurrentFocus}";
        OpenIssueCount = _stateQuery.GetOpenIssues().Count;
        ResumeSummary = string.IsNullOrWhiteSpace(resumePoint?.Summary)
            ? "No resume point recorded."
            : resumePoint.Summary;
        NextAction = string.IsNullOrWhiteSpace(resumePoint?.NextRecommendedAction)
            ? "—"
            : resumePoint.NextRecommendedAction;

        RecentWork.Clear();
        foreach (var engagement in _stateQuery.GetRecentEngagements(5))
        {
            var title = string.IsNullOrWhiteSpace(engagement.TaskDescription)
                ? string.IsNullOrWhiteSpace(engagement.WorkerName) ? "Engineering activity" : engagement.WorkerName
                : engagement.TaskDescription;
            var summary = string.IsNullOrWhiteSpace(engagement.Summary)
                ? engagement.Outcome.ToString()
                : engagement.Summary;
            var status = engagement.Acceptance == AcceptanceStatus.Accepted
                ? "Accepted"
                : engagement.Outcome.ToString();
            RecentWork.Add(new RecentWorkItemViewModel(
                title,
                summary,
                status,
                engagement.StartedUtc.ToLocalTime().ToString("g"),
                engagement.FilesChanged?.Count ?? 0));
        }

        if (RecentWork.Count == 0 && resumePoint is not null &&
            (!string.IsNullOrWhiteSpace(resumePoint.Summary) || !string.IsNullOrWhiteSpace(resumePoint.NextRecommendedAction)))
        {
            RecentWork.Add(new RecentWorkItemViewModel(
                "Resume point",
                string.IsNullOrWhiteSpace(resumePoint.Summary) ? "Work can be resumed from the recorded next action." : resumePoint.Summary,
                "Ready to resume",
                resumePoint.CapturedUtc.ToLocalTime().ToString("g"),
                0));
        }

        if (RecentWork.Count == 0)
        {
            RecentWork.Add(new RecentWorkItemViewModel(
                "No recent work recorded",
                "Meaningful worker activity will appear here as engineering work progresses.",
                "Waiting",
                "—",
                0));
        }

        OnPropertyChanged(nameof(RecentWork));
    }

    private async Task RefreshGitStatusAsync()
    {
        var version = Interlocked.Increment(ref _refreshVersion);
        var repositoryPath = _stateQuery.GetWorkspaceContext()?.RepositoryPath;
        var snapshot = await _gitStatusService.GetStatusAsync(repositoryPath ?? string.Empty);
        if (version == Volatile.Read(ref _refreshVersion))
        {
            GitStatus = snapshot;
        }
    }

    private void WorkspaceState_OnChange()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            _ = RefreshAsync();
            return;
        }

        dispatcher.BeginInvoke(new Action(() => _ = RefreshAsync()));
    }

    private void SetProperty<T>(ref T field, T value, string propertyName)
    {
        if (Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record RecentWorkItemViewModel(
    string Title,
    string Summary,
    string Status,
    string When,
    int FilesChanged)
{
    public string FilesChangedText => FilesChanged == 1 ? "1 file" : $"{FilesChanged} files";
}
