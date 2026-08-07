using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using System.Diagnostics;

namespace EngineeringDiscovery.Wpf.ViewModels
{
    public class ConversationMessage
    {
        public string Speaker { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool>? _canExecute;

        public Task? LastTask { get; private set; }

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter)
        {
            LastTask = _execute();
            _ = LastTask;
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class WorkspaceConversationViewModel : INotifyPropertyChanged
    {
        private static int _instanceCounter = 0;
        private readonly int _instanceId;
        private readonly IEngineeringPartner _partner;

        public ObservableCollection<ConversationMessage> Messages { get; } = new ObservableCollection<ConversationMessage>();

        private string _draft = string.Empty;
        public string Draft
        {
            get => _draft;
            set
            {
                if (_draft != value)
                {
                    _draft = value;
                    OnPropertyChanged(nameof(Draft));
                    (SendCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            private set
            {
                if (_isSending != value)
                {
                    _isSending = value;
                    OnPropertyChanged(nameof(IsSending));
                    (SendCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public Guid? SessionId { get; private set; }

        private bool _initialized = false;

        public ICommand SendCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        // Event raised when the conversation messages change. The WorkspaceViewModel subscribes and
        // uses this to coordinate package lifecycle (mark Needs Review when appropriate).
        public event Action? MessagesChanged;

        public WorkspaceConversationViewModel(IEngineeringPartner partner)
        {
            _instanceId = System.Threading.Interlocked.Increment(ref _instanceCounter);
            _partner = partner ?? throw new ArgumentNullException(nameof(partner));
            SendCommand = new AsyncRelayCommand(async () => await SendCurrentMessageAsync(), () => !IsSending && !string.IsNullOrWhiteSpace(Draft));

            // Observe own Messages collection for changes and notify the host workspace via an event.
            Messages.CollectionChanged += (s, e) => OnMessagesChanged();

            Debug.WriteLine($"[ED-EP7] WorkspaceConversationViewModel #{_instanceId} created");
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void OnMessagesChanged()
        {
            MessagesChanged?.Invoke();
            // Publish a ConversationUpdated engineering event for the engine to observe
            EngineeringDiscovery.Wpf.Events.EngineeringEventBus.Publish(new EngineeringDiscovery.Wpf.Events.EngineeringEvent(EngineeringDiscovery.Wpf.Events.EngineeringEventType.ConversationUpdated, null));
        }

        public async Task InitializeAsync(string openingStatement = "")
        {
            if (_initialized) return;
            _initialized = true;

            Debug.WriteLine($"[ED-EP7] InitializeAsync #{_instanceId} started");
            try
            {
                Debug.WriteLine($"[ED-EP7] Calling StartSessionAsync from VM #{_instanceId} with openingStatement='{openingStatement}'");
                var model = await _partner.StartSessionAsync(openingStatement);
                SessionId = model?.Id ?? Guid.Empty;

                if (model?.Conversation != null && model.Conversation.Any())
                {
                    foreach (var e in model.Conversation)
                    {
                        Messages.Add(new ConversationMessage { Speaker = e.Speaker ?? string.Empty, Text = e.Message ?? string.Empty, TimestampUtc = e.TimestampUtc });
                    }
                }
                else
                {
                    Messages.Add(new ConversationMessage { Speaker = "Engineering Partner", Text = "Good morning. What are we working on today?", TimestampUtc = DateTime.UtcNow });
                }

                Debug.WriteLine($"[ED-EP7] InitializeAsync #{_instanceId} Session created: {SessionId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ED-EP7] InitializeAsync #{_instanceId} failed: " + ex);
                Messages.Add(new ConversationMessage { Speaker = "Engineering Partner", Text = "Hello — the Engineering Partner is currently unavailable. You can still type messages and they will be sent when the service returns.", TimestampUtc = DateTime.UtcNow });
            }
            Debug.WriteLine($"[ED-EP7] InitializeAsync #{_instanceId} complete");
        }

        public async Task SendCurrentMessageAsync()
        {
            var text = (Draft ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                Debug.WriteLine("[ED-EP5.1] Ignored empty message send");
                return;
            }

            Debug.WriteLine("[ED-EP5.1] User message sending: " + text);

            var userMsg = new ConversationMessage { Speaker = "You", Text = text, TimestampUtc = DateTime.UtcNow };
            Messages.Add(userMsg);
            Draft = string.Empty;

            if (SessionId == null || SessionId == Guid.Empty)
            {
                try
                {
                    var m = await _partner.StartSessionAsync(string.Empty);
                    SessionId = m?.Id ?? Guid.Empty;
                    Debug.WriteLine("[ED-EP5.1] Session created on send: " + SessionId);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[ED-EP5.1] Failed to start session on send: " + ex);
                    Messages.Add(new ConversationMessage { Speaker = "Engineering Partner", Text = "Unable to start session. Try again later.", TimestampUtc = DateTime.UtcNow });
                    return;
                }
            }

            IsSending = true;
            try
            {
                Debug.WriteLine("[ED-EP5.1] Calling partner.SendMessageAsync");
                var reply = await _partner.SendMessageAsync(SessionId.Value, text);
                Debug.WriteLine("[ED-EP5.1] Partner response received");
                if (!string.IsNullOrWhiteSpace(reply))
                {
                    Messages.Add(new ConversationMessage { Speaker = "Engineering Partner", Text = reply, TimestampUtc = DateTime.UtcNow });
                }
                else
                {
                    Messages.Add(new ConversationMessage { Speaker = "Engineering Partner", Text = "(no reply was returned)", TimestampUtc = DateTime.UtcNow });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ED-EP5.1] Error during SendMessageAsync: " + ex);
                Messages.Add(new ConversationMessage { Speaker = "Engineering Partner", Text = "An error occurred while sending the message. Please try again.", TimestampUtc = DateTime.UtcNow });
            }
            finally
            {
                IsSending = false;
            }
            Debug.WriteLine("[ED-EP5.1] Conversation updated");
        }
    }
}
