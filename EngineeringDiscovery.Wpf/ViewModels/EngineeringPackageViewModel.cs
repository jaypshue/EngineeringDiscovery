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

        private string _status = "Draft";
        public string Status
        {
            get => _status;
            set { if (_status != value) { _status = value; OnPropertyChanged(nameof(Status)); OnPackageContentChanged(); if (_status == "Ready for Review" || _status == "Ready for Implementation") { IsDirty = false; } } }
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

        public ObservableCollection<EngineeringPackageContextItem> ContextIncluded { get; } = new ObservableCollection<EngineeringPackageContextItem>()
        {
            new EngineeringPackageContextItem { Text = "Current Investigation", Included = true },
            new EngineeringPackageContextItem { Text = "Repository", Included = true },
            new EngineeringPackageContextItem { Text = "Engineering Model", Included = true },
            new EngineeringPackageContextItem { Text = "Architecture Decisions", Included = true },
            new EngineeringPackageContextItem { Text = "Evidence", Included = true }
        };

        private DateTime _lastUpdated = DateTime.UtcNow;
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set { if (_lastUpdated != value) { _lastUpdated = value; OnPropertyChanged(nameof(LastUpdated)); } }
        }

        public ICommand PreviewCommand { get; }
        public ICommand SendToCopilotCommand { get; }
        public ICommand ChangeStatusCommand { get; }

        public EngineeringPackageViewModel()
        {
            this.PreviewCommand = new RelayCommand(o => Preview());
            this.SendToCopilotCommand = new RelayCommand(o => SendToCopilot());
            this.ChangeStatusCommand = new RelayCommand(o => ChangeStatus(o as string ?? "Draft"));
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
            Status = status;
            LastUpdated = DateTime.UtcNow;
        }

        private void OnPackageContentChanged()
        {
            // Package content updated: increment version and mark dirty
            Version++;
            IsDirty = true;
            LastUpdated = DateTime.UtcNow;
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
