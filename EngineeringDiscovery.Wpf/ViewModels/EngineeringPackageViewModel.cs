using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows;

namespace EngineeringDiscovery.Wpf.ViewModels
{
    public class EngineeringPackageContextItem
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _text = string.Empty;
        public string Text { get => _text; set { if (_text != value) { _text = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text))); } } }

        private bool _included = true;
        public bool Included { get => _included; set { if (_included != value) { _included = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Included))); } } }
    }

    public class EngineeringPackageViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        // Status constants to avoid magic strings
        public const string StatusDraft = "Draft";
        public const string StatusCollecting = "Collecting Context";
        public const string StatusReadyForReview = "Ready for Review";
        public const string StatusReadyForImplementation = "Ready for Implementation";
        public const string StatusNeedsReview = "Needs Review";

        private string _status = StatusDraft;
        public string Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        private string _purpose = "(purpose placeholder)";
        public string Purpose
        {
            get => _purpose;
            set { if (_purpose != value) { _purpose = value; OnPropertyChanged(nameof(Purpose)); OnPackageContentChanged(); } }
        }

        private string _markdown = "# Engineering Package\n\nThis is a placeholder package.";
        public string Markdown
        {
            get => _markdown;
            set { if (_markdown != value) { _markdown = value; OnPropertyChanged(nameof(Markdown)); OnPackageContentChanged(); } }
        }

        private int _version = 1;
        public int Version { get => _version; private set { if (_version != value) { _version = value; OnPropertyChanged(nameof(Version)); } } }

        private bool _isDirty = false;
        public bool IsDirty { get => _isDirty; private set { if (_isDirty != value) { _isDirty = value; OnPropertyChanged(nameof(IsDirty)); } } }

        private bool _isReadyForImplementation = false;
        public bool IsReadyForImplementation { get => _isReadyForImplementation; private set { if (_isReadyForImplementation != value) { _isReadyForImplementation = value; OnPropertyChanged(nameof(IsReadyForImplementation)); } } }

        // ContextIncluded is owned and populated by the workspace; package observes the collection for meaningful changes
        public ObservableCollection<EngineeringPackageContextItem> ContextIncluded { get; } = new ObservableCollection<EngineeringPackageContextItem>();

        private DateTime _lastUpdated = DateTime.UtcNow;
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set { if (_lastUpdated != value) { _lastUpdated = value; OnPropertyChanged(nameof(LastUpdated)); } }
        }

        // ReviewedVersion: when Version == ReviewedVersion the package may be considered reviewed/current
        public int ReviewedVersion { get; private set; } = 0;

        private DateTime? _reviewedTimestamp;
        public DateTime? ReviewedTimestamp { get => _reviewedTimestamp; private set { if (_reviewedTimestamp != value) { _reviewedTimestamp = value; OnPropertyChanged(nameof(ReviewedTimestamp)); } } }

        // Additional status constants for workflow
        public const string StatusImplementationPending = "Implementation Pending";
        public const string StatusImplementationIncorporated = "Implementation Incorporated";

        public ICommand PreviewCommand { get; }
        public ICommand SendToCopilotCommand { get; }
        public ICommand ChangeStatusCommand { get; }

        public EngineeringPackageViewModel()
        {
            this.PreviewCommand = new RelayCommand(o => Preview());
            this.SendToCopilotCommand = new RelayCommand(o => SendToCopilot());
            this.ChangeStatusCommand = new RelayCommand(o => ChangeStatus(o as string ?? "Draft"));

            // Observe context changes to mark meaningful package updates
            ContextIncluded.CollectionChanged += (s, e) =>
            {
                // subscribe to new items property changes
                if (e.NewItems != null)
                {
                    foreach (var ni in e.NewItems)
                    {
                        if (ni is EngineeringPackageContextItem item)
                        {
                            item.PropertyChanged += ContextItem_PropertyChanged;
                        }
                    }
                }

                // unsubscribe removed items
                if (e.OldItems != null)
                {
                    foreach (var oi in e.OldItems)
                    {
                        if (oi is EngineeringPackageContextItem item)
                        {
                            item.PropertyChanged -= ContextItem_PropertyChanged;
                        }
                    }
                }

                // Treat collection membership changes as meaningful
                OnPackageContentChanged();
            };
        }

        // Public workflow operations invoked by the workspace (placeholder implementations)
        public void Generate()
        {
            // Simulate generation: treat as meaningful content change
            OnPackageContentChanged();
            // After generation, mark ready for review
            Status = StatusReadyForReview;
            LastUpdated = DateTime.UtcNow;
        }

        public void Approve()
        {
            // Record review metadata and transition to Ready for Implementation
            ReviewedVersion = Version;
            ReviewedTimestamp = DateTime.UtcNow;
            IsDirty = false;
            Status = StatusReadyForImplementation;
            LastUpdated = DateTime.UtcNow;
        }

        public void MarkImplementationPending()
        {
            Status = StatusImplementationPending;
            LastUpdated = DateTime.UtcNow;
        }

        public void IncorporateImplementation()
        {
            Status = StatusImplementationIncorporated;
            LastUpdated = DateTime.UtcNow;
            // mark as not dirty
            IsDirty = false;
        }

        public void Regenerate()
        {
            // Re-generate package: create a new meaningful content update and make it ready for review
            OnPackageContentChanged();
            Status = StatusReadyForReview;
            LastUpdated = DateTime.UtcNow;
        }

        private void ContextItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Any change to a context item is a meaningful package change
            OnPackageContentChanged();
        }

        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Preview()
        {
            // Show a simple dialog with markdown read-only content
            var win = new System.Windows.Window
            {
                Title = "Engineering Package Preview",
                Width = 800,
                Height = 600,
                Content = new System.Windows.Controls.TextBox { Text = Markdown, IsReadOnly = true, TextWrapping = System.Windows.TextWrapping.Wrap, VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto }
            };
            win.ShowDialog();
        }

        public void SendToCopilot()
        {
            try
            {
                System.Windows.Clipboard.SetText(Markdown ?? string.Empty);
            }
            catch
            {
                // ignore clipboard errors (presentation-only)
            }
        }

        public void ChangeStatus(string status)
        {
            // Change status without treating status change as an engineering content change
            Status = status ?? StatusDraft;
            LastUpdated = DateTime.UtcNow;

            // If marking as ready, record the reviewed version and clear dirty flag
            if (status == StatusReadyForReview || status == StatusReadyForImplementation)
            {
                ReviewedVersion = Version;
                IsDirty = false;
                LastUpdated = DateTime.UtcNow;
            }
            else if (status == StatusDraft || status == StatusCollecting)
            {
                // no special action
            }
            else if (status == StatusNeedsReview)
            {
                IsDirty = true;
                LastUpdated = DateTime.UtcNow;
            }
        }

        private void OnPackageContentChanged()
        {
            // Package content updated: increment version and mark dirty
            Version++;
            IsDirty = true;
            LastUpdated = DateTime.UtcNow;

            // If this change makes the Version newer than the reviewed version, mark as Needs Review
            if (ReviewedVersion > 0 && Version > ReviewedVersion)
            {
                Status = StatusNeedsReview;
                IsDirty = true;
                LastUpdated = DateTime.UtcNow;
            }
        }
    }

    // Minimal RelayCommand for presentation actions
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _action;
        private readonly Func<object?, bool>? _can;
        public RelayCommand(Action<object?> action, Func<object?, bool>? can = null) { _action = action; _can = can; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _can?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _action(parameter);
    }
}
