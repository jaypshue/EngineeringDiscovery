using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineeringDiscovery.Core.Services;
using System;

namespace EngineeringDiscovery.Wpf.ViewModels
{
    public class EngineeringWorkspaceViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private readonly EngineeringDiscovery.Core.Services.WorkspaceState? _workspaceState;

        private string _repositoryName = string.Empty;
        public string RepositoryName
        {
            get => _repositoryName;
            private set { _repositoryName = value; OnPropertyChanged(nameof(RepositoryName)); }
        }

        private string _repositoryPath = string.Empty;
        public string RepositoryPath
        {
            get => _repositoryPath;
            private set { _repositoryPath = value; OnPropertyChanged(nameof(RepositoryPath)); }
        }

        private string _repositoryStatus = string.Empty;
        public string RepositoryStatus
        {
            get => _repositoryStatus;
            private set { _repositoryStatus = value; OnPropertyChanged(nameof(RepositoryStatus)); }
        }

        public WorkspaceConversationViewModel Conversation { get; }

        public EngineeringPackageViewModel Package { get; }

        // PrimaryAction exposed for binding: workspace determines the available primary action and command
        public string PrimaryActionText { get; private set; } = string.Empty;
        public ICommand? PrimaryActionCommand { get; private set; }

        public EngineeringStateViewModel EngineeringState { get; private set; }

        public EngineeringWorkspaceViewModel(IEngineeringPartner partner)
        {
            if (partner == null) throw new ArgumentNullException(nameof(partner));

            // Create child VMs synchronously so they are fully initialized before exposure to the view
            Package = new EngineeringPackageViewModel();
            Conversation = new WorkspaceConversationViewModel(partner);

            // Asynchronously initialize conversation without blocking construction
            _ = InitializeAsync();

            // Subscribe to conversation change events to coordinate package lifecycle
            Conversation.MessagesChanged += HandleConversationMessagesChanged;

            // Initialize the primary action binding
            UpdatePrimaryAction();

            // Create and expose EngineeringState projection
            EngineeringState = new EngineeringStateViewModel(Conversation, Package);

            // Expose an EngineeringEngine for hosts to route intent through the Engine orchestration boundary
            Engine = new Services.EngineeringEngine(new Services.RepositoryDiscoveryService());

            // If a workspace has already been selected (imported), publish a RepositoryDiscovered event so
            // EngineeringStateViewModel picks up the repository immediately and the UI shows it.
            try
            {
                var sp = EngineeringDiscovery.Wpf.App.ServiceProvider;
                var ws = sp?.GetService(typeof(EngineeringDiscovery.Core.Services.WorkspaceState)) as EngineeringDiscovery.Core.Services.WorkspaceState;
                _workspaceState = ws;
                // subscribe to workspace changes so repository selection is reflected in the UI
                if (_workspaceState != null)
                {
                    _workspaceState.OnChange += WorkspaceState_OnChange;
                }

                var repoPath = ws?.ActiveWorkspace?.RepositoryPath;
                if (!string.IsNullOrWhiteSpace(repoPath))
                {
                    EngineeringDiscovery.Wpf.Events.EngineeringEventBus.Publish(new EngineeringDiscovery.Wpf.Events.EngineeringEvent(EngineeringDiscovery.Wpf.Events.EngineeringEventType.RepositoryDiscovered, repoPath));
                    EngineeringDiscovery.Wpf.Events.EngineeringEventBus.Publish(new EngineeringDiscovery.Wpf.Events.EngineeringEvent(EngineeringDiscovery.Wpf.Events.EngineeringEventType.RepositoryAnalysisCompleted, new { Repository = repoPath, SolutionCount = 0, ProjectCount = 0 }));
                    // initialize displayed repository properties
                    RepositoryPath = repoPath;
                    RepositoryName = System.IO.Path.GetFileName(repoPath.TrimEnd(System.IO.Path.DirectorySeparatorChar));
                    RepositoryStatus = EngineeringState.CurrentReadiness;
                }
            }
            catch
            {
                // ignore any service resolution issues during construction
            }
        }

        public Services.EngineeringEngine Engine { get; }

        private async Task InitializeAsync()
        {
            await Conversation.InitializeAsync();

            // After conversation init, perform any package sync on UI thread via Application.Current.Dispatcher
            try
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Minimal initial package sync
                        Package.Purpose = $"Implement {Package.Version} – Live Engineering Package";
                    }));
                }
                else
                {
                    Package.Purpose = $"Implement {Package.Version} – Live Engineering Package";
                }
            }
            catch
            {
                // ignore dispatcher issues during early initialization
            }
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void WorkspaceState_OnChange()
        {
            try
            {
                if (_workspaceState is null) return;
                var repo = _workspaceState.ActiveWorkspace?.RepositoryPath;
                // marshal to UI thread if available
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.BeginInvoke(new Action(() => UpdateRepositoryFromWorkspace(repo)));
                }
                else
                {
                    UpdateRepositoryFromWorkspace(repo);
                }
            }
            catch
            {
                // ignore
            }
        }

        private void UpdateRepositoryFromWorkspace(string? repo)
        {
            if (string.IsNullOrWhiteSpace(repo))
            {
                RepositoryPath = string.Empty;
                RepositoryName = string.Empty;
                RepositoryStatus = EngineeringState.CurrentReadiness;
            }
            else
            {
                RepositoryPath = repo;
                RepositoryName = System.IO.Path.GetFileName(repo.TrimEnd(System.IO.Path.DirectorySeparatorChar));
                RepositoryStatus = EngineeringState.CurrentReadiness;
            }
        }

        private void HandleConversationMessagesChanged()
        {
            // If the package has been reviewed (ReviewedVersion > 0) and conversation changes, mark Needs Review
            if (Package.ReviewedVersion > 0 && Package.Version >= Package.ReviewedVersion)
            {
                // Transition to Needs Review if not already
                if (Package.Status != EngineeringPackageViewModel.StatusNeedsReview)
                {
                    // Marshal to UI thread
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher != null && !dispatcher.CheckAccess())
                    {
                        dispatcher.BeginInvoke(new Action(() => Package.ChangeStatus(EngineeringPackageViewModel.StatusNeedsReview)));
                    }
                    else
                    {
                        Package.ChangeStatus(EngineeringPackageViewModel.StatusNeedsReview);
                    }
                }
            }

            // Update recommended action when conversation changes
            UpdatePrimaryAction();
        }

        private void UpdatePrimaryAction()
        {
            // Determine recommended action and primary action command based on current package status
            var status = Package.Status;
            if (status == EngineeringPackageViewModel.StatusDraft)
            {
                PrimaryActionText = "Generate Package";
                PrimaryActionCommand = new RelayCommand(_ => Package.Generate());
            }
            else if (status == EngineeringPackageViewModel.StatusCollecting)
            {
                PrimaryActionText = "Collect Context";
                PrimaryActionCommand = null;
            }
            else if (status == EngineeringPackageViewModel.StatusReadyForReview)
            {
                PrimaryActionText = "Approve Package";
                PrimaryActionCommand = new RelayCommand(_ => { Package.Approve(); UpdatePrimaryAction(); });
            }
            else if (status == EngineeringPackageViewModel.StatusReadyForImplementation)
            {
                PrimaryActionText = "Send to Copilot";
                PrimaryActionCommand = new RelayCommand(_ => { /* workspace-level send handled by Package command */ });
            }
            else if (status == EngineeringPackageViewModel.StatusNeedsReview)
            {
                PrimaryActionText = "Regenerate Package";
                PrimaryActionCommand = new RelayCommand(_ => { Package.Regenerate(); UpdatePrimaryAction(); });
            }
            else
            {
                PrimaryActionText = "Generate Package";
                PrimaryActionCommand = new RelayCommand(_ => Package.Generate());
            }

            OnPropertyChanged(nameof(PrimaryActionText));
            OnPropertyChanged(nameof(PrimaryActionCommand));
        }
    }
}
