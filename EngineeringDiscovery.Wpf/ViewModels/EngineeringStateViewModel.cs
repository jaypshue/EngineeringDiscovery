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

        // Evidence collection owned by Engineering State
        public System.Collections.ObjectModel.ObservableCollection<EngineeringDiscovery.Wpf.Models.EngineeringEvidence> Evidence { get; } = new System.Collections.ObjectModel.ObservableCollection<EngineeringDiscovery.Wpf.Models.EngineeringEvidence>();

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

            // Initialize evidence with placeholder categories (missing vs present)
            SeedInitialEvidence();

            // Initial evaluation of engineering state
            EvaluateEngineeringState();
        }

        private void SeedInitialEvidence()
        {
            // Seed placeholder evidence entries; the Engine will update these when events occur
            Evidence.Add(new EngineeringDiscovery.Wpf.Models.EngineeringEvidence { Category = EngineeringDiscovery.Wpf.Models.EvidenceCategory.Repository, Title = "Repository", Status = "Present" });
            Evidence.Add(new EngineeringDiscovery.Wpf.Models.EngineeringEvidence { Category = EngineeringDiscovery.Wpf.Models.EvidenceCategory.Conversation, Title = "Conversation", Status = "Present" });
            Evidence.Add(new EngineeringDiscovery.Wpf.Models.EngineeringEvidence { Category = EngineeringDiscovery.Wpf.Models.EvidenceCategory.Architecture, Title = "Architecture", Status = "Partial" });
            Evidence.Add(new EngineeringDiscovery.Wpf.Models.EngineeringEvidence { Category = EngineeringDiscovery.Wpf.Models.EvidenceCategory.Build, Title = "Build", Status = "Missing" });
            Evidence.Add(new EngineeringDiscovery.Wpf.Models.EngineeringEvidence { Category = EngineeringDiscovery.Wpf.Models.EvidenceCategory.Tests, Title = "Tests", Status = "Missing" });
            Evidence.Add(new EngineeringDiscovery.Wpf.Models.EngineeringEvidence { Category = EngineeringDiscovery.Wpf.Models.EvidenceCategory.Screenshots, Title = "Screenshots", Status = "Missing" });
        }

        private void OnArtifactsChanged()
        {
            EvaluateEngineeringState();
        }

        private void OnEngineeringEvent(EngineeringDiscovery.Wpf.Events.EngineeringEvent evt)
        {
            // When an engineering event is observed, re-evaluate engineering state
            // Convert certain events into evidence updates
            if (evt.Type == EngineeringDiscovery.Wpf.Events.EngineeringEventType.ConversationUpdated)
            {
                UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory.Conversation, "Conversation", "Conversation updated", "Present", evt.TimestampUtc);
            }
            else if (evt.Type == EngineeringDiscovery.Wpf.Events.EngineeringEventType.PackageApproved)
            {
                UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory.PackageReview, "Package Review", "Package approved", "Present", evt.TimestampUtc);
            }

            EvaluateEngineeringState();
        }

        private void UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory category, string title, string description, string status, DateTime timestamp)
        {
            // Try to find existing evidence by category and title
            var existing = System.Linq.Enumerable.FirstOrDefault(Evidence, e => e.Category == category && e.Title == title);
            if (existing != null)
            {
                existing.Description = description;
                existing.Status = status;
                existing.TimestampUtc = timestamp;
                // Notify collection changed by replacing item (simple approach)
                var idx = Evidence.IndexOf(existing);
                if (idx >= 0)
                {
                    Evidence[idx] = existing;
                }
            }
            else
            {
                Evidence.Add(new EngineeringDiscovery.Wpf.Models.EngineeringEvidence { Category = category, Title = title, Description = description, Status = status, TimestampUtc = timestamp });
            }
            OnPropertyChanged(nameof(Evidence));
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
