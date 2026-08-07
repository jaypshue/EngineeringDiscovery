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
        public EngineeringDiscovery.Wpf.Models.RepositoryInfo RepositoryModel { get; } = new EngineeringDiscovery.Wpf.Models.RepositoryInfo();
        public EngineeringDiscovery.Wpf.Models.EngineeringUnderstanding Understanding { get; } = new EngineeringDiscovery.Wpf.Models.EngineeringUnderstanding();

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

            // Subscribe to engineering events for repository discovery and model updates
            EngineeringDiscovery.Wpf.Events.EngineeringEventBus.Subscribe(OnEngineeringEvent);

            // Initialize evidence with placeholder categories (missing vs present)
            SeedInitialEvidence();

            // Initial evaluation of engineering state
            EvaluateEngineeringState();
        }

        private void EvaluateUnderstanding()
        {
            // Derive simple understanding from RepositoryModel
            Understanding.SolutionCount = RepositoryModel.SolutionCount;
            Understanding.ProjectCount = RepositoryModel.ProjectCount;
            Understanding.IsRepositoryIndexed = RepositoryModel.ProjectCount > 0 || RepositoryModel.SolutionCount > 0;
            Understanding.IsMultiProjectSolution = RepositoryModel.SolutionCount > 0 && RepositoryModel.ProjectCount > 1;

            // Simple platform detection heuristics (placeholder): look for project file names or paths containing known markers
            Understanding.DetectedPlatforms.Clear();
            foreach (var p in RepositoryModel.ProjectPaths)
            {
                var fn = System.IO.Path.GetFileName(p).ToLowerInvariant();
                if (fn.Contains("wpf") || fn.Contains("*.wpf") || p.ToLowerInvariant().Contains("windowsdesktop"))
                {
                    if (!Understanding.DetectedPlatforms.Contains("WPF")) Understanding.DetectedPlatforms.Add("WPF");
                }
                if (fn.Contains("aspnet") || fn.Contains("web") || p.ToLowerInvariant().Contains("microsoft.aspnetcore"))
                {
                    if (!Understanding.DetectedPlatforms.Contains("ASP.NET Core")) Understanding.DetectedPlatforms.Add("ASP.NET Core");
                }
            }

            // Update evidence summary for understanding
            if (Understanding.IsRepositoryIndexed)
            {
                UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory.Repository, "Repository Indexed", $"Solutions: {Understanding.SolutionCount}, Projects: {Understanding.ProjectCount}", "Present", DateTime.UtcNow);
            }

            if (Understanding.DetectedPlatforms.Count > 0)
            {
                UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory.Repository, "Platforms Detected", string.Join(", ", Understanding.DetectedPlatforms), "Present", DateTime.UtcNow);
            }
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
            EvaluateUnderstanding();
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
            else if (evt.Type == EngineeringDiscovery.Wpf.Events.EngineeringEventType.RepositoryDiscovered)
            {
                UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory.Repository, "Repository", $"Repository selected: {evt.Payload}", "Present", evt.TimestampUtc);
            }
            else if (evt.Type == EngineeringDiscovery.Wpf.Events.EngineeringEventType.SolutionDiscovered)
            {
                UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory.Repository, "Solution", $"Solution: {evt.Payload}", "Present", evt.TimestampUtc);
                // Update repository model
                if (evt.Payload is string sln)
                {
                    RepositoryModel.SolutionNames.Add(System.IO.Path.GetFileName(sln));
                    RepositoryModel.RepositoryRoot = System.IO.Path.GetDirectoryName(sln) ?? RepositoryModel.RepositoryRoot;
                }
            }
            else if (evt.Type == EngineeringDiscovery.Wpf.Events.EngineeringEventType.ProjectDiscovered)
            {
                UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory.Repository, "Project", $"Project: {evt.Payload}", "Present", evt.TimestampUtc);
                if (evt.Payload is string proj)
                {
                    RepositoryModel.ProjectPaths.Add(proj);
                    RepositoryModel.RepositoryRoot = RepositoryModel.RepositoryRoot ?? System.IO.Path.GetDirectoryName(proj) ?? RepositoryModel.RepositoryRoot;
                }
            }
            else if (evt.Type == EngineeringDiscovery.Wpf.Events.EngineeringEventType.RepositoryAnalysisCompleted)
            {
                UpsertEvidence(EngineeringDiscovery.Wpf.Models.EvidenceCategory.Repository, "Repository Analysis", "Repository analysis completed", "Present", evt.TimestampUtc);
                // If payload contains summary counts, reflect them in repository model title evidence
                if (evt.Payload is { } payload)
                {
                    try
                    {
                        var repo = payload.GetType().GetProperty("Repository")?.GetValue(payload) as string;
                        var solCountObj = payload.GetType().GetProperty("SolutionCount")?.GetValue(payload);
                        var projCountObj = payload.GetType().GetProperty("ProjectCount")?.GetValue(payload);
                        if (!string.IsNullOrEmpty(repo)) RepositoryModel.RepositoryRoot = repo;
                        if (solCountObj is int solCount)
                        {
                            // nothing to do for now beyond storing counts in the model lists
                        }
                        if (projCountObj is int projCount)
                        {
                            // nothing to do for now
                        }
                    }
                    catch
                    {
                        // ignore reflection problems on anonymous payload
                    }
                }
            }

            EvaluateEngineeringState();
            EvaluateUnderstanding();
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
            // Notify that evidence and derived properties may have changed
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Evidence)));
        }

        private void EvaluateEngineeringState()
        {
            // Engineering State should primarily consume the derived EngineeringUnderstanding
            // Keep logic intentionally small: prefer understanding-derived values, fall back to package status.
            if (Understanding.IsRepositoryIndexed)
            {
                CurrentPhase = Understanding.IsMultiProjectSolution ? "Repository" : "Understanding";
                CurrentReadiness = "Repository Indexed";

                // Recommend investigation based on detected platforms when available
                if (Understanding.DetectedPlatforms.Contains("WPF"))
                {
                    CurrentRecommendation = "Investigate WPF application";
                    PrimaryActionText = "Review WPF Project";
                    PrimaryActionCommand = null;
                }
                else if (Understanding.DetectedPlatforms.Contains("ASP.NET Core"))
                {
                    CurrentRecommendation = "Investigate ASP.NET Core application";
                    PrimaryActionText = "Review Web Project";
                    PrimaryActionCommand = null;
                }
                else
                {
                    CurrentRecommendation = "Review repository structure";
                    PrimaryActionText = "Generate Package";
                    PrimaryActionCommand = new RelayCommand(_ => { Package.Generate(); EvaluateEngineeringState(); });
                }
            }
            else
            {
                // Fall back to package-driven state when no repository understanding exists
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
