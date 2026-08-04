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
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Intent));
        }

        public string Title { get; private set; } = string.Empty;
        public string Type { get; private set; } = string.Empty;
        public string Status { get; private set; } = string.Empty;
        public string Intent { get; private set; } = string.Empty;
    }
}
