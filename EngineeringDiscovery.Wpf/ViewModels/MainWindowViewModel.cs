using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EngineeringDiscovery.Core.Services;

// Add dispatcher for marshaling UI updates
using System.Windows.Threading;

namespace EngineeringDiscovery.Wpf.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly WorkspaceState _workspaceState;
    private bool _disposed;

    public MainWindowViewModel(WorkspaceState workspaceState, EngineeringDiscovery.Wpf.Services.RepositorySelectionService repoSelection, ActivityViewModel activityViewModel)
    {
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        _repoSelection = repoSelection ?? throw new ArgumentNullException(nameof(repoSelection));
        Activity = activityViewModel ?? throw new ArgumentNullException(nameof(activityViewModel));

        NewInvestigationCommand = new RelayCommand(o => System.Windows.MessageBox.Show("New Investigation (placeholder)"));
        OpenRepositoryCommand = new AsyncRelayCommand(async () => await OpenRepositoryAsync());
        ExitCommand = new RelayCommand(o => System.Windows.Application.Current.Shutdown());

        // Initialize HasWorkspace based on current state and subscribe to changes
        HasWorkspace = _workspaceState.HasWorkspace;
        _workspaceState.OnChange += WorkspaceState_OnChange;

        // Subscribe to repo selection state so UI can react while startup flow runs
        _repoSelection.StateChanged += RepoSelection_StateChanged;
    }

    public ActivityViewModel Activity { get; }

    private readonly EngineeringDiscovery.Wpf.Services.RepositorySelectionService _repoSelection;

    private async Task OpenRepositoryAsync()
    {
        // Start the native folder picker and detection flow
        await _repoSelection.PickFolderAsync();

        // Wait for server-side detection to complete (simple loop, short timeout)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (_repoSelection.IsDetecting && sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(100);
        }

        if (_repoSelection.IsImportEnabled)
        {
            var ok = await _repoSelection.ImportAsync();
            if (!ok)
            {
                System.Windows.MessageBox.Show(_repoSelection.ErrorMessage ?? "Import failed", "Import Failed");
            }
        }
        else
        {
            // Show error or guidance
            System.Windows.MessageBox.Show(_repoSelection.ErrorMessage ?? "No supported repository detected.", "Import Not Available");
        }
    }

    public ICommand NewInvestigationCommand { get; }
    public ICommand OpenRepositoryCommand { get; }
    public ICommand ExitCommand { get; }

    public bool HasWorkspace { get; private set; }
    public bool HasNoWorkspace => !HasWorkspace;

    private void WorkspaceState_OnChange()
    {
        // Marshal to UI thread if available
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

    private void RefreshProjection()
    {
        HasWorkspace = _workspaceState.HasWorkspace;
        OnPropertyChanged(nameof(HasWorkspace));
        OnPropertyChanged(nameof(HasNoWorkspace));
    }

    private void RepoSelection_StateChanged()
    {
        // Trigger UI refresh when repository selection state changes
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

    public void Dispose()
    {
        if (_disposed) return;
        _workspaceState.OnChange -= WorkspaceState_OnChange;
        _disposed = true;
    }
}
