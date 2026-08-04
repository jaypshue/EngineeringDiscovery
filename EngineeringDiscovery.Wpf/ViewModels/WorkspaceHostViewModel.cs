using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Wpf.ViewModels;

public partial class WorkspaceHostViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceState _workspaceState;
    private bool _disposed;

    public WorkspaceHostViewModel(WorkspaceState workspaceState, EngineeringDiscovery.Wpf.Services.RepositorySelectionService repoSelection)
    {
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        Title = "Workspace Host";

        // Subscribe to Core state changes
        _workspaceState.OnChange += WorkspaceState_OnChange;

        // Subscribe to repository selection interaction changes (startup flow)
        repoSelection.StateChanged += RepoSelection_StateChanged;

        // Initialize projection
        RefreshProjection();
    }

    public string Title { get; set; }

    private void WorkspaceState_OnChange()
    {
        // If there is no WPF Application (unit tests or non-UI host), invoke refresh directly
        if (System.Windows.Application.Current is null)
        {
            RefreshProjection();
            return;
        }

        // Marshal to UI thread if necessary
        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            RefreshProjection();
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)RefreshProjection, DispatcherPriority.Normal);
    }

    private void RefreshProjection()
    {
        // Project lightweight UI properties from the core state without copying domain objects
        // Example: raise PropertyChanged for UI bindings that read directly from WorkspaceState.ActiveWorkspace
        OnPropertyChanged(nameof(RepositoryPath));
    }

    private void RepoSelection_StateChanged()
    {
        // Marshal to UI thread if necessary
        if (System.Windows.Application.Current is null)
        {
            RefreshProjection();
            return;
        }

        if (System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            RefreshProjection();
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)RefreshProjection, DispatcherPriority.Normal);
    }

    public string? RepositoryPath => _workspaceState.ActiveWorkspace?.RepositoryPath;

    public void Dispose()
    {
        if (_disposed) return;
        _workspaceState.OnChange -= WorkspaceState_OnChange;
        _disposed = true;
    }
}
