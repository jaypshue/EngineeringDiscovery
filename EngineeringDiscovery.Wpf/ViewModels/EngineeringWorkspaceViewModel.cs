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
        }

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
