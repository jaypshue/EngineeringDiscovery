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
    private readonly IWorkspacePersistence _persistence;
    private readonly AsyncRelayCommand _openProjectCommand;
    private bool _isLoading;
    private string _loadingStatusText = string.Empty;
    private bool _disposed;

    public MainWindowViewModel(
        WorkspaceState workspaceState,
        EngineeringDiscovery.Wpf.Services.RepositorySelectionService repoSelection,
        ActivityViewModel activityViewModel,
        IWorkspacePersistence persistence)
    {
        _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        _repoSelection = repoSelection ?? throw new ArgumentNullException(nameof(repoSelection));
        Activity = activityViewModel ?? throw new ArgumentNullException(nameof(activityViewModel));
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));

        NewInvestigationCommand = new RelayCommand(o => System.Windows.MessageBox.Show("New Investigation (placeholder)"));
        _openProjectCommand = new AsyncRelayCommand(async () => await OpenRepositoryAsync(), () => !IsLoading);
        OpenRepositoryCommand = _openProjectCommand;
        CloseProjectCommand = new RelayCommand(_ => CloseProject());
        ExitCommand = new RelayCommand(o => System.Windows.Application.Current.Shutdown());

        // Initialize HasWorkspace based on current state and subscribe to changes
        HasWorkspace = _workspaceState.HasWorkspace;
        HasActiveProject = _workspaceState.ActiveWorkspace?.ActiveProject is not null;
        _workspaceState.OnChange += WorkspaceState_OnChange;

        // Subscribe to repo selection state so UI can react while startup flow runs
        _repoSelection.StateChanged += RepoSelection_StateChanged;
    }

    public ActivityViewModel Activity { get; }

    private readonly EngineeringDiscovery.Wpf.Services.RepositorySelectionService _repoSelection;

    public event Action? RepositoryImported;

    public ICommand NewInvestigationCommand { get; }
    public ICommand OpenRepositoryCommand { get; }
    public ICommand CloseProjectCommand { get; }
    public ICommand ExitCommand { get; }

    public bool HasWorkspace { get; private set; }
    public bool HasActiveProject { get; private set; }
    public bool HasNoWorkspace => !HasActiveProject;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged(nameof(IsLoading));
            _openProjectCommand.RaiseCanExecuteChanged();
        }
    }

    public string LoadingStatusText
    {
        get => _loadingStatusText;
        private set
        {
            if (_loadingStatusText == value) return;
            _loadingStatusText = value;
            OnPropertyChanged(nameof(LoadingStatusText));
        }
    }

    public bool CloseProject()
    {
        if (!HasActiveProject) return true;

        var emptyWorkspace = new global::EngineeringDiscovery.Core.Domain.Workspace.Workspace();
        try
        {
            _persistence.SaveAsync(emptyWorkspace).GetAwaiter().GetResult();
            _workspaceState.ReplaceWorkspace(emptyWorkspace);
            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"EngineOS could not close the project: {ex.Message}", "Close Project");
            return false;
        }
    }

    private async Task OpenRepositoryAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        LoadingStatusText = "Opening project…";
        try
        {
            // Start the native folder picker and detection flow.
            await _repoSelection.PickFolderAsync();

            LoadingStatusText = "Inspecting repository…";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (_repoSelection.IsDetecting && sw.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(100);
            }

            if (_repoSelection.IsImportEnabled)
            {
                LoadingStatusText = "Loading engineering context…";
                await CompleteSelectedRepositoryImportAsync();
            }
            else if (!string.IsNullOrWhiteSpace(_repoSelection.SelectedPath))
            {
                System.Windows.MessageBox.Show(_repoSelection.ErrorMessage ?? "No supported repository detected.", "Import Not Available");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Import failed: {ex.Message}", "Open Repository");
        }
        finally
        {
            IsLoading = false;
            LoadingStatusText = string.Empty;
        }
    }

    /// <summary>
    /// Completes the existing selected-folder import path. The picker remains owned by
    /// OpenRepositoryAsync; this seam also keeps the successful import boundary testable.
    /// </summary>
    public async Task<bool> CompleteSelectedRepositoryImportAsync(bool showErrors = true)
    {
        var ownsLoadingState = !IsLoading;
        if (ownsLoadingState)
        {
            IsLoading = true;
            LoadingStatusText = "Loading engineering context…";
        }

        try
        {
            return await CompleteSelectedRepositoryImportCoreAsync(showErrors);
        }
        finally
        {
            if (ownsLoadingState)
            {
                IsLoading = false;
                LoadingStatusText = string.Empty;
            }
        }
    }

    private async Task<bool> CompleteSelectedRepositoryImportCoreAsync(bool showErrors)
    {
        if (!_repoSelection.IsImportEnabled)
        {
            if (showErrors)
            {
                System.Windows.MessageBox.Show(_repoSelection.ErrorMessage ?? "No supported repository detected.", "Import Not Available");
            }
            return false;
        }

        var ok = await _repoSelection.ImportAsync();
        if (!ok)
        {
            if (showErrors)
            {
                System.Windows.MessageBox.Show(_repoSelection.ErrorMessage ?? "Import failed", "Import Failed");
            }
            return false;
        }

        RepositoryImported?.Invoke();
        return true;
    }

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
        HasActiveProject = _workspaceState.ActiveWorkspace?.ActiveProject is not null;
        OnPropertyChanged(nameof(HasWorkspace));
        OnPropertyChanged(nameof(HasActiveProject));
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
        _repoSelection.StateChanged -= RepoSelection_StateChanged;
        RepositoryImported = null;
        _disposed = true;
    }
}
