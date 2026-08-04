using System;
using CommunityToolkit.Mvvm.ComponentModel;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Wpf.ViewModels
{
    public class ActivityViewModel : ObservableObject
    {
        private readonly WorkspaceState _workspaceState;

        public ActivityViewModel(WorkspaceState workspaceState)
        {
            _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
            _workspaceState.OnChange += WorkspaceState_OnChange;
            Refresh();
        }

        private void WorkspaceState_OnChange() => Refresh();

        private void Refresh()
        {
            var act = _workspaceState.CurrentActivity;
            Title = act?.Title ?? string.Empty;
            Type = act?.ActivityType.ToString() ?? string.Empty;
            Status = act?.Status.ToString() ?? string.Empty;
            Intent = act is null ? string.Empty : string.Join("\n", act.Intent?.ToArray() ?? System.Array.Empty<string>());
            CurrentObservationDescription = act?.CurrentObservation?.Description ?? string.Empty;
            CurrentObservationSource = act?.CurrentObservation?.Source.ToString() ?? string.Empty;
            CurrentObservationConfidence = act?.CurrentObservation?.Confidence ?? 0;
            CurrentHypothesisDescription = act?.CurrentHypothesis?.Description ?? string.Empty;
            CurrentHypothesisStatus = act?.CurrentHypothesis?.Status.ToString() ?? string.Empty;
            CurrentHypothesisConfidence = act?.CurrentHypothesis?.Confidence ?? 0;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Intent));
            OnPropertyChanged(nameof(CurrentObservationDescription));
            OnPropertyChanged(nameof(CurrentObservationSource));
            OnPropertyChanged(nameof(CurrentObservationConfidence));
            OnPropertyChanged(nameof(CurrentHypothesisDescription));
            OnPropertyChanged(nameof(CurrentHypothesisStatus));
            OnPropertyChanged(nameof(CurrentHypothesisConfidence));
        }

        public string Title { get; private set; } = string.Empty;
        public string Type { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public string Intent { get; private set; } = string.Empty;
        public string CurrentObservationDescription { get; private set; } = string.Empty;
        public string CurrentObservationSource { get; private set; } = string.Empty;
        public int CurrentObservationConfidence { get; private set; }
        public string CurrentHypothesisDescription { get; private set; } = string.Empty;
        public string CurrentHypothesisStatus { get; private set; } = string.Empty;
        public int CurrentHypothesisConfidence { get; private set; }
    }
}
