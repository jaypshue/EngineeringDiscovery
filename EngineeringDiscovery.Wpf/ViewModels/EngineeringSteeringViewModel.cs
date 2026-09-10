using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EngineeringDiscovery.Core.Domain.CurrentTask;
using EngineeringDiscovery.Core.Domain.Iteration;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Wpf.ViewModels;

/// <summary>
/// Read-only WPF projection of engineering direction and round history.
/// It deliberately does not execute a round: Phase 1 establishes the steering
/// workflow while repository commands and agent execution remain separate concerns.
/// </summary>
public sealed class EngineeringSteeringViewModel : ObservableObject, IDisposable
{
    private readonly IEngineeringStateQuery _stateQuery;
    private readonly WorkspaceState _workspaceState;
    private HandoffState? _promptHandoff;
    private string _currentDirection = "No current direction established.";
    private string _currentTaskTitle = string.Empty;
    private string _currentRound = "No active round";
    private string _currentRoundGoal = "Development has not started.";
    private string _currentRoundStatus = "No active round recorded";
    private string _lastCompleted = "No completed work recorded.";
    private string _nextHandoff = "No clear next step.";
    private string _handoffStatus = "No clear next step";
    private string _confidence = "Low — human direction required";
    private string _humanAttention = "🔴 Human intervention required — Establish a clear direction.";
    private string _promptText = "Generate a prompt when a handoff is available.";
    private string _promptStatus = "No prompt generated.";
    private string _promptReadiness = "No clear next handoff established.";
    private string _promptAssociation = "No round prompt recorded.";
    private string _currentRoundSummary = "No round review recorded.";
    private string _currentRoundChanges = "Not captured in the round record. Use Changes evidence.";
    private string _currentRoundTests = "Not captured in the round record. Use Results evidence.";
    private string _currentRoundProblems = "Not captured in the round record. Use Problems evidence.";
    private string _currentRoundAgentResponse = "No agent response captured.";
    private string _currentRoundAssessment = "No EngineOS assessment recorded.";
    private Guid? _promptRoundId;
    private bool _hasPromptArtifact;
    private bool _disposed;
    private bool _hasDevelopmentRounds;

    public EngineeringSteeringViewModel(IEngineeringStateQuery stateQuery, WorkspaceState workspaceState)
    {
        _stateQuery = stateQuery ?? throw new ArgumentNullException(nameof(stateQuery));
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));

        DevelopmentRounds = new ObservableCollection<DevelopmentRoundViewModel>();
        GeneratePromptCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(GeneratePrompt, CanGeneratePrompt);

        _workspaceState.OnChange += WorkspaceState_OnChange;
        Refresh();
    }

    public ObservableCollection<DevelopmentRoundViewModel> DevelopmentRounds { get; }

    public bool HasDevelopmentRounds
    {
        get => _hasDevelopmentRounds;
        private set
        {
            if (!SetProperty(ref _hasDevelopmentRounds, value)) return;
            OnPropertyChanged(nameof(HasNoDevelopmentRounds));
        }
    }

    public bool HasNoDevelopmentRounds => !HasDevelopmentRounds;

    public ICommand GeneratePromptCommand { get; }

    public string CurrentDirection
    {
        get => _currentDirection;
        private set => SetProperty(ref _currentDirection, value);
    }

    public string CurrentTaskTitle
    {
        get => _currentTaskTitle;
        private set => SetProperty(ref _currentTaskTitle, value);
    }

    public string CurrentRound
    {
        get => _currentRound;
        private set => SetProperty(ref _currentRound, value);
    }

    public string CurrentRoundGoal
    {
        get => _currentRoundGoal;
        private set => SetProperty(ref _currentRoundGoal, value);
    }

    public string CurrentRoundStatus
    {
        get => _currentRoundStatus;
        private set => SetProperty(ref _currentRoundStatus, value);
    }

    public string LastCompleted
    {
        get => _lastCompleted;
        private set => SetProperty(ref _lastCompleted, value);
    }

    public string NextHandoff
    {
        get => _nextHandoff;
        private set => SetProperty(ref _nextHandoff, value);
    }

    public string HandoffStatus
    {
        get => _handoffStatus;
        private set => SetProperty(ref _handoffStatus, value);
    }

    public string Confidence
    {
        get => _confidence;
        private set => SetProperty(ref _confidence, value);
    }

    public string HumanAttention
    {
        get => _humanAttention;
        private set => SetProperty(ref _humanAttention, value);
    }

    public string PromptText
    {
        get => _promptText;
        private set => SetProperty(ref _promptText, value);
    }

    public string PromptStatus
    {
        get => _promptStatus;
        private set => SetProperty(ref _promptStatus, value);
    }

    public bool HasPromptArtifact
    {
        get => _hasPromptArtifact;
        private set
        {
            if (!SetProperty(ref _hasPromptArtifact, value)) return;
            OnPropertyChanged(nameof(HasNoPromptArtifact));
        }
    }

    public bool HasNoPromptArtifact => !HasPromptArtifact;

    public string PromptReadiness
    {
        get => _promptReadiness;
        private set => SetProperty(ref _promptReadiness, value);
    }

    public string PromptAssociation
    {
        get => _promptAssociation;
        private set => SetProperty(ref _promptAssociation, value);
    }

    public string CurrentRoundSummary
    {
        get => _currentRoundSummary;
        private set => SetProperty(ref _currentRoundSummary, value);
    }

    public string CurrentRoundChanges
    {
        get => _currentRoundChanges;
        private set => SetProperty(ref _currentRoundChanges, value);
    }

    public string CurrentRoundTests
    {
        get => _currentRoundTests;
        private set => SetProperty(ref _currentRoundTests, value);
    }

    public string CurrentRoundProblems
    {
        get => _currentRoundProblems;
        private set => SetProperty(ref _currentRoundProblems, value);
    }

    public string CurrentRoundAgentResponse
    {
        get => _currentRoundAgentResponse;
        private set => SetProperty(ref _currentRoundAgentResponse, value);
    }

    public string CurrentRoundAssessment
    {
        get => _currentRoundAssessment;
        private set => SetProperty(ref _currentRoundAssessment, value);
    }

    /// <summary>
    /// Rebuilds the projection from current authoritative state. Public visibility
    /// keeps the mapping directly testable without constructing a WPF window.
    /// </summary>
    public void Refresh()
    {
        var workspace = _workspaceState.ActiveWorkspace;
        var projectState = workspace?.ActiveProjectState ?? workspace?.ProjectState;
        var lifecycle = _stateQuery.GetLifecycle();
        var resumePoint = _stateQuery.GetResumePoint();
        var currentTask = workspace?.CurrentTask;
        var openIssues = _stateQuery.GetOpenIssues() ?? Array.Empty<KnownIssue>();
        var engagements = _stateQuery.GetRecentEngagements(20) ?? Array.Empty<WorkerEngagement>();

        CurrentTaskTitle = string.IsNullOrWhiteSpace(currentTask?.Title) ? string.Empty : currentTask.Title;
        CurrentDirection = ResolveDirection(lifecycle, resumePoint, currentTask);

        var iterations = (workspace?.Iterations ?? new System.Collections.Generic.List<EngineeringIteration>())
            .Where(iteration => iteration is not null)
            .OrderBy(iteration => iteration.CreatedUtc)
            .ToList();

        var activeRound = iterations.LastOrDefault(iteration => iteration.Status != IterationStatus.Completed);

        DevelopmentRounds.Clear();
        for (var index = 0; index < iterations.Count; index++)
        {
            DevelopmentRounds.Add(new DevelopmentRoundViewModel(index + 1, iterations[index], iterations[index] == activeRound));
        }
        HasDevelopmentRounds = iterations.Count > 0;
        if (activeRound is null)
        {
            CurrentRound = "No active round";
            CurrentRoundGoal = "Development has not started.";
            CurrentRoundStatus = "No active round recorded";
        }
        else
        {
            var activeNumber = iterations.IndexOf(activeRound) + 1;
            CurrentRound = $"Round {activeNumber}";
            CurrentRoundGoal = string.IsNullOrWhiteSpace(activeRound.Goal) ? "No round goal recorded." : activeRound.Goal;
            CurrentRoundStatus = FormatIterationStatus(activeRound.Status);
        }

        var completedRound = iterations.LastOrDefault(iteration => iteration.Status == IterationStatus.Completed);
        if (completedRound is not null)
        {
            var completedNumber = iterations.IndexOf(completedRound) + 1;
            var goal = string.IsNullOrWhiteSpace(completedRound.Goal) ? "Unnamed development round" : completedRound.Goal;
            LastCompleted = $"Round {completedNumber} — {goal} · ✓ Complete";
        }
        else
        {
            var completedEngagement = engagements
                .Where(engagement => engagement.Outcome == EngagementOutcome.Completed && engagement.Acceptance == AcceptanceStatus.Accepted)
                .OrderByDescending(engagement => engagement.CompletedUtc ?? engagement.StartedUtc)
                .FirstOrDefault();
            if (completedEngagement is not null)
            {
                var title = string.IsNullOrWhiteSpace(completedEngagement.TaskDescription)
                    ? "Accepted engineering work"
                    : completedEngagement.TaskDescription;
                LastCompleted = $"{title} · ✓ Accepted";
            }
            else
            {
                var completedCapability = (projectState?.CompletedCapabilities ?? new System.Collections.Generic.List<CompletedCapability>())
                    .Where(capability => capability.Acceptance == CapabilityAcceptance.Accepted)
                    .OrderByDescending(capability => capability.CompletedUtc)
                    .FirstOrDefault();
                LastCompleted = completedCapability is null
                    ? "No completed work recorded."
                    : $"{completedCapability.Title} · ✓ Accepted";
            }
        }

        var reviewRound = activeRound ?? iterations.LastOrDefault();
        UpdateRoundReview(reviewRound);
        RestorePromptArtifact(iterations, activeRound);

        var currentHandoff = _stateQuery.GetCurrentHandoff();
        if (currentHandoff is not null && !string.IsNullOrWhiteSpace(currentHandoff.Objective))
        {
            _promptHandoff = currentHandoff;
            NextHandoff = currentHandoff.Objective;
            HandoffStatus = "Ready";
        }
        else
        {
            _promptHandoff = null;
            NextHandoff = "No clear next handoff established.";
            HandoffStatus = "Human decision required";
        }

        UpdateConfidenceAndAttention(lifecycle, openIssues, engagements);
        UpdatePromptState();
        (GeneratePromptCommand as CommunityToolkit.Mvvm.Input.RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void UpdateRoundReview(EngineeringIteration? round)
    {
        var step = round?.Steps?.LastOrDefault();
        CurrentRoundSummary = string.IsNullOrWhiteSpace(step?.HumanObservation)
            ? "No round summary recorded."
            : step.HumanObservation;
        CurrentRoundAgentResponse = string.IsNullOrWhiteSpace(step?.CopilotResponse)
            ? "No agent response captured."
            : step.CopilotResponse;
        CurrentRoundAssessment = string.IsNullOrWhiteSpace(step?.Assessment)
            ? "No EngineOS assessment recorded."
            : step.Assessment;
        CurrentRoundChanges = "Not captured in the round record. Use Changes evidence.";
        CurrentRoundTests = "Not captured in the round record. Use Results evidence.";
        CurrentRoundProblems = "Not captured in the round record. Use Problems evidence.";
    }

    private void RestorePromptArtifact(
        System.Collections.Generic.IReadOnlyList<EngineeringIteration> iterations,
        EngineeringIteration? activeRound)
    {
        var promptStep = activeRound?.Steps?
            .LastOrDefault(step => !string.IsNullOrWhiteSpace(step.Prompt));
        if (promptStep is not null)
        {
            PromptText = promptStep.Prompt;
            HasPromptArtifact = true;
            _promptRoundId = activeRound!.Id;
            PromptAssociation = $"Preserved in Round {iterations.ToList().IndexOf(activeRound) + 1}.";
            PromptStatus = "Prompt preserved in the current round.";
            return;
        }

        if (_hasPromptArtifact && (!_promptRoundId.HasValue || activeRound?.Id == _promptRoundId)) return;

        PromptText = "Generate a prompt when a handoff is available.";
        HasPromptArtifact = false;
        _promptRoundId = null;
        PromptAssociation = "No round prompt recorded.";
    }

    private static string ResolveDirection(ProjectLifecycle? lifecycle, ResumePoint? resumePoint, CurrentTask? currentTask)
    {
        if (!string.IsNullOrWhiteSpace(lifecycle?.CurrentFocus)) return lifecycle.CurrentFocus;
        if (!string.IsNullOrWhiteSpace(currentTask?.Brief?.Objective)) return currentTask.Brief.Objective;
        if (!string.IsNullOrWhiteSpace(currentTask?.Goal)) return currentTask.Goal;
        if (!string.IsNullOrWhiteSpace(resumePoint?.NextRecommendedAction)) return resumePoint.NextRecommendedAction;
        return "No current direction established.";
    }

    private void UpdateConfidenceAndAttention(
        ProjectLifecycle? lifecycle,
        System.Collections.Generic.IReadOnlyList<KnownIssue> openIssues,
        System.Collections.Generic.IReadOnlyList<WorkerEngagement> engagements)
    {
        var criticalIssue = openIssues.FirstOrDefault(issue => issue.Severity == IssueSeverity.Critical);
        var failedEngagement = engagements.FirstOrDefault(engagement =>
            engagement.Outcome is EngagementOutcome.Failed or EngagementOutcome.Escalated);
        var partialEngagement = engagements.FirstOrDefault(engagement => engagement.Outcome == EngagementOutcome.PartiallyCompleted);
        var rejectedEngagement = engagements.FirstOrDefault(engagement => engagement.Acceptance == AcceptanceStatus.Rejected);
        var directionIsMissing = CurrentDirection == "No current direction established.";
        var handoffIsMissing = HandoffStatus != "Ready";

        if (directionIsMissing || handoffIsMissing || criticalIssue is not null || failedEngagement is not null)
        {
            Confidence = directionIsMissing || criticalIssue is not null || failedEngagement is not null
                ? "Low — human direction required"
                : "Low — next handoff is not established";
            HumanAttention = criticalIssue is not null
                ? $"🔴 Serious problem detected — Critical issue: {criticalIssue.Title}"
                : failedEngagement is not null
                    ? $"🔴 Human decision required — Review {failedEngagement.Outcome.ToString().ToLowerInvariant()} work."
                    : directionIsMissing
                        ? "🔴 Direction unclear — establish the current direction."
                        : "🟡 Human decision required — Establish the next handoff.";
            return;
        }

        if (lifecycle?.Phase == LifecyclePhase.Paused || partialEngagement is not null || rejectedEngagement is not null || HandoffStatus != "Ready")
        {
            Confidence = "Medium — review recommended";
            HumanAttention = lifecycle?.Phase == LifecyclePhase.Paused
                ? "🟡 Review recommended — Development is paused."
                : partialEngagement is not null
                    ? "🟡 Review recommended — The latest work is partially complete."
                    : rejectedEngagement is not null
                        ? "🟡 Review recommended — The latest work was rejected."
                        : "🟡 Review recommended — Assemble or confirm the next handoff.";
            return;
        }

        Confidence = "High — ready to continue";
        HumanAttention = "🟢 No attention required";
    }

    private bool CanGeneratePrompt() => _promptHandoff is not null;

    private void GeneratePrompt()
    {
        if (_promptHandoff is null) return;

        var workspace = _workspaceState.ActiveWorkspace;
        var projectState = workspace?.ActiveProjectState ?? workspace?.ProjectState;
        PromptText = EngineeringPromptGenerator.GeneratePrompt(_promptHandoff, projectState);
        HasPromptArtifact = true;
        var roundNumber = PersistPromptToActiveRound();
        PromptAssociation = roundNumber.HasValue
            ? $"Preserved in Round {roundNumber.Value}."
            : "Generated for this handoff; no active round record exists to preserve it.";
        PromptStatus = roundNumber.HasValue
            ? "Prompt generated and preserved in the current round."
            : "Prompt generated. No active round record exists yet.";
        UpdatePromptState();
    }

    private int? PersistPromptToActiveRound()
    {
        var workspace = _workspaceState.ActiveWorkspace;
        if (workspace?.Iterations is null) return null;

        var ordered = workspace.Iterations
            .Where(iteration => iteration is not null)
            .OrderBy(iteration => iteration.CreatedUtc)
            .ToList();
        var activeRound = ordered.LastOrDefault(iteration => iteration.Status != IterationStatus.Completed);
        if (activeRound is null) return null;

        activeRound.Steps ??= new System.Collections.Generic.List<EngineeringIterationStep>();
        var step = activeRound.Steps.LastOrDefault();
        if (step is null || !string.IsNullOrWhiteSpace(step.Prompt) || !string.IsNullOrWhiteSpace(step.CopilotResponse))
        {
            step = new EngineeringIterationStep();
            activeRound.Steps.Add(step);
        }

        step.Prompt = PromptText;
        _promptRoundId = activeRound.Id;
        _workspaceState.PersistAndNotify();
        return ordered.IndexOf(activeRound) + 1;
    }

    private void UpdatePromptState()
    {
        PromptReadiness = HasPromptArtifact
            ? _promptHandoff is null
                ? "Prompt preserved; review the recorded artifact."
                : "Prompt ready for review"
            : _promptHandoff is null
                ? "No clear next handoff established."
                : "Ready to generate";

        if (!HasPromptArtifact)
        {
            PromptStatus = _promptHandoff is null
                ? "Prompt unavailable until a handoff is established."
                : "Prompt available to generate.";
        }
    }

    private static string FormatIterationStatus(IterationStatus status) => status switch
    {
        IterationStatus.InProgress => "● In Progress",
        IterationStatus.Paused => "Ⅱ Paused",
        IterationStatus.Completed => "✓ Complete",
        _ => status.ToString()
    };

    private void WorkspaceState_OnChange()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Refresh();
        else dispatcher.BeginInvoke(new Action(Refresh));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _workspaceState.OnChange -= WorkspaceState_OnChange;
        _disposed = true;
    }
}

public sealed class DevelopmentRoundViewModel
{
    public DevelopmentRoundViewModel(int number, EngineeringIteration iteration, bool isCurrent)
    {
        Number = number;
        IsCurrent = isCurrent;
        IsHistorical = !isCurrent;
        Goal = string.IsNullOrWhiteSpace(iteration.Goal) ? "Unnamed development round" : iteration.Goal;
        Status = iteration.Status switch
        {
            IterationStatus.InProgress => "● In Progress",
            IterationStatus.Paused => "Ⅱ Paused",
            IterationStatus.Completed => "✓ Complete",
            _ => iteration.Status.ToString()
        };
        Created = iteration.CreatedUtc.ToLocalTime().ToString("g");
        StartCompletionState = iteration.Status == IterationStatus.Completed
            ? "Completed state recorded; completion time not captured."
            : $"Started {Created}";
        StepCount = $"{iteration.Steps?.Count ?? 0} step(s)";
        var latestStep = iteration.Steps is { Count: > 0 } ? iteration.Steps[^1] : null;
        PromptStatus = iteration.Steps?.Any(step => !string.IsNullOrWhiteSpace(step.Prompt)) == true
            ? "Prompt preserved"
            : "Prompt not recorded";
        ReviewSummary = string.IsNullOrWhiteSpace(latestStep?.HumanObservation)
            ? "No round summary recorded."
            : latestStep.HumanObservation;
        AgentResponse = string.IsNullOrWhiteSpace(latestStep?.CopilotResponse)
            ? "No agent response captured."
            : latestStep.CopilotResponse;
        Assessment = string.IsNullOrWhiteSpace(latestStep?.Assessment)
            ? "No EngineOS assessment recorded."
            : latestStep.Assessment;
        LatestStep = latestStep is null ? "No round steps recorded." : ResolveLatestStep(latestStep);
    }

    public int Number { get; }
    public bool IsCurrent { get; }
    public bool IsHistorical { get; }
    public string Goal { get; }
    public string Status { get; }
    public string Created { get; }
    public string StartCompletionState { get; }
    public string StepCount { get; }
    public string PromptStatus { get; }
    public string ReviewSummary { get; }
    public string AgentResponse { get; }
    public string Assessment { get; }
    public string LatestStep { get; }

    private static string ResolveLatestStep(EngineeringIterationStep step)
    {
        if (!string.IsNullOrWhiteSpace(step.NextAction)) return $"Next: {step.NextAction}";
        if (!string.IsNullOrWhiteSpace(step.HumanObservation)) return step.HumanObservation;
        if (!string.IsNullOrWhiteSpace(step.Assessment)) return step.Assessment;
        if (!string.IsNullOrWhiteSpace(step.CopilotResponse)) return step.CopilotResponse;
        return string.IsNullOrWhiteSpace(step.Prompt) ? "Step recorded without a summary." : step.Prompt;
    }
}
