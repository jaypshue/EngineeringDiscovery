using System;
using System.ComponentModel;
using System.Windows.Input;

namespace EngineeringDiscovery.Wpf.ViewModels
{
    // Represents the canonical Engineering State for the current workspace.
    // Owns references to artifacts (Conversation, Package) and projects their combined
    // status into high-level engineering properties used by the UI.
    public class EngineeringStateViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public WorkspaceConversationViewModel Conversation { get; }
        public EngineeringPackageViewModel Package { get; }

        public Investigation CurrentInvestigation { get; }

        public string CurrentPhase { get; private set; } = "Discovery";
        public string CurrentReadiness { get; private set; } = "Not Ready";
        public string CurrentRecommendation { get; private set; } = string.Empty;

        public string PrimaryActionText { get; private set; } = string.Empty;
        public ICommand? PrimaryActionCommand { get; private set; }

        public EngineeringStateViewModel(WorkspaceConversationViewModel conversation, EngineeringPackageViewModel package)
        {
            Conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
            Package = package ?? throw new ArgumentNullException(nameof(package));

            // Lightweight investigation placeholder
            CurrentInvestigation = new Investigation { Name = "ED-600 – Active Investigation", Description = "(placeholder)", Status = "Open" };

            // Subscribe to artifact changes
            Conversation.MessagesChanged += OnArtifactsChanged;
            Package.PropertyChanged += (s, e) => OnArtifactsChanged();

            // Initial evaluation of engineering state
            EvaluateEngineeringState();
        }

        private void OnArtifactsChanged()
        {
            ComputeState();
        }

        private void EvaluateEngineeringState()
        {
            // Phase derived from package status (simplified mapping)
            var ps = Package.Status;
            if (ps == EngineeringPackageViewModel.StatusDraft)
            {
                CurrentPhase = "Investigation";
                CurrentReadiness = "Not Ready";
                CurrentRecommendation = "Generate Package";
                PrimaryActionText = "Generate Package";
                PrimaryActionCommand = new RelayCommand(_ => { Package.Generate(); EvaluateEngineeringState(); });
            }
            else if (ps == EngineeringPackageViewModel.StatusCollecting)
            {
                CurrentPhase = "Understanding";
                CurrentReadiness = "Collecting Context";
                CurrentRecommendation = "Collect Evidence";
                PrimaryActionText = "Collect Context";
                PrimaryActionCommand = null;
            }
            else if (ps == EngineeringPackageViewModel.StatusReadyForReview)
            {
                CurrentPhase = "Package Review";
                CurrentReadiness = "Ready for Review";
                CurrentRecommendation = "Review Package";
                PrimaryActionText = "Approve Package";
                PrimaryActionCommand = new RelayCommand(_ => { Package.Approve(); EvaluateEngineeringState(); });
            }
            else if (ps == EngineeringPackageViewModel.StatusReadyForImplementation)
            {
                CurrentPhase = "Implementation";
                CurrentReadiness = "Ready for Implementation";
                CurrentRecommendation = "Send to Copilot";
                PrimaryActionText = "Send to Copilot";
                PrimaryActionCommand = new RelayCommand(_ => { Package.SendToCopilot(); });
            }
            else if (ps == EngineeringPackageViewModel.StatusNeedsReview)
            {
                CurrentPhase = "Review";
                CurrentReadiness = "Needs Review";
                CurrentRecommendation = "Regenerate Package";
                PrimaryActionText = "Regenerate Package";
                PrimaryActionCommand = new RelayCommand(_ => { Package.Regenerate(); EvaluateEngineeringState(); });
            }
            else
            {
                CurrentPhase = "Investigation";
                CurrentReadiness = "Not Ready";
                CurrentRecommendation = "Generate Package";
                PrimaryActionText = "Generate Package";
                PrimaryActionCommand = new RelayCommand(_ => { Package.Generate(); EvaluateEngineeringState(); });
            }

            // Notify UI
            OnPropertyChanged(nameof(CurrentPhase));
            OnPropertyChanged(nameof(CurrentReadiness));
            OnPropertyChanged(nameof(CurrentRecommendation));
            OnPropertyChanged(nameof(PrimaryActionText));
            OnPropertyChanged(nameof(PrimaryActionCommand));
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class Investigation
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
