using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Models;
using EngineeringDiscovery.Wpf.Services;

namespace EngineeringDiscovery.Wpf.ViewModels
{
    public class ConversationMessage
    {
        public string Speaker { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public ConversationResponseKind ResponseKind { get; set; } = ConversationResponseKind.Informational;
        public string? ProposedAction { get; set; }
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
        private readonly IStructuredConversationPartner? _structuredPartner;
        private readonly IConfirmedEngineeringOperationExecutor? _operationExecutor;
        private readonly SemaphoreSlim _operationGate = new(1, 1);

        private string _draft = string.Empty;
        private string? _pendingAction;
        private EngineeringOperationKind? _pendingOperation;
        private bool _isSending;
        private bool _isConfirming;
        private bool _isInitializing;
        private bool _initializationFailed;
        private bool _initialized;

        public ObservableCollection<ConversationMessage> Messages { get; } = new ObservableCollection<ConversationMessage>();

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

        public bool IsSending
        {
            get => _isSending;
            private set
            {
                if (_isSending == value) return;
                _isSending = value;
                OnPropertyChanged(nameof(IsSending));
                NotifyBusyStateChanged();
            }
        }

        public bool IsInitializing
        {
            get => _isInitializing;
            private set
            {
                if (_isInitializing == value) return;
                _isInitializing = value;
                OnPropertyChanged(nameof(IsInitializing));
                NotifyBusyStateChanged();
            }
        }

        public bool IsConfirming
        {
            get => _isConfirming;
            private set
            {
                if (_isConfirming == value) return;
                _isConfirming = value;
                OnPropertyChanged(nameof(IsConfirming));
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(StatusText));
                (SendCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                (ConfirmActionCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool IsBusy => IsInitializing || IsSending || IsConfirming;

        public string StatusText
        {
            get
            {
                if (IsInitializing) return "Connecting to EngineOS…";
                if (IsSending) return "EngineOS is thinking…";
                if (IsConfirming) return "Executing the confirmed operation…";
                if (_initializationFailed) return "Conversation is unavailable. Send a message to retry.";
                return !SessionId.HasValue || SessionId.Value == Guid.Empty ? "Ready to connect" : "Ready";
            }
        }

        public Guid? SessionId { get; private set; }

        public ICommand SendCommand { get; }
        public ICommand ConfirmActionCommand { get; }

        public string? PendingAction
        {
            get => _pendingAction;
            private set
            {
                if (_pendingAction == value) return;
                _pendingAction = value;
                OnPropertyChanged(nameof(PendingAction));
                OnPropertyChanged(nameof(HasPendingConfirmation));
                OnPropertyChanged(nameof(ConfirmationText));
                (ConfirmActionCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public bool HasPendingConfirmation => !string.IsNullOrWhiteSpace(PendingAction);
        public string ConfirmationText => HasPendingConfirmation
            ? $"Confirmation required before: {PendingAction}"
            : string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Event raised when the conversation messages change. The WorkspaceViewModel subscribes and
        // uses this to coordinate package lifecycle (mark Needs Review when appropriate).
        public event Action? MessagesChanged;

        public WorkspaceConversationViewModel(
            IEngineeringPartner partner,
            IStructuredConversationPartner? structuredPartner = null,
            IConfirmedEngineeringOperationExecutor? operationExecutor = null)
        {
            _instanceId = Interlocked.Increment(ref _instanceCounter);
            _partner = partner ?? throw new ArgumentNullException(nameof(partner));
            _structuredPartner = structuredPartner ?? partner as IStructuredConversationPartner;
            _operationExecutor = operationExecutor;
            SendCommand = new AsyncRelayCommand(
                SendCurrentMessageAsync,
                () => !IsBusy && !string.IsNullOrWhiteSpace(Draft));
            ConfirmActionCommand = new AsyncRelayCommand(
                ConfirmPendingActionAsync,
                () => HasPendingConfirmation && !IsConfirming);

            // Observe own Messages collection for changes and notify the host workspace via an event.
            Messages.CollectionChanged += (s, e) => OnMessagesChanged();

            Debug.WriteLine($"[ED-EP7] WorkspaceConversationViewModel #{_instanceId} created");
        }

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void NotifyBusyStateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(StatusText));
            (SendCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SetSession(Guid? sessionId)
        {
            SessionId = sessionId;
            OnPropertyChanged(nameof(SessionId));
            OnPropertyChanged(nameof(StatusText));
        }

        private void OnMessagesChanged()
        {
            MessagesChanged?.Invoke();
            // Publish a ConversationUpdated engineering event for the engine to observe
            EngineeringDiscovery.Wpf.Events.EngineeringEventBus.Publish(new EngineeringDiscovery.Wpf.Events.EngineeringEvent(EngineeringDiscovery.Wpf.Events.EngineeringEventType.ConversationUpdated, null));
        }

        public async Task InitializeAsync(string openingStatement = "")
        {
            if (_initialized || IsInitializing) return;

            await _operationGate.WaitAsync().ConfigureAwait(true);
            try
            {
                if (_initialized) return;

                IsInitializing = true;
                _initializationFailed = false;
                OnPropertyChanged(nameof(StatusText));

                Debug.WriteLine($"[ED-EP7] InitializeAsync #{_instanceId} started");
                Debug.WriteLine($"[ED-EP7] Calling StartSessionAsync from VM #{_instanceId} with openingStatement='{openingStatement}'");
                var model = await _partner.StartSessionAsync(openingStatement);
                if (model is null)
                {
                    _initializationFailed = true;
                    Messages.Add(new ConversationMessage
                    {
                        Speaker = "Engineering Partner",
                        Text = "Hello — the Engineering Partner is currently unavailable. You can still type a question and send it to retry the connection.",
                        TimestampUtc = DateTime.UtcNow
                    });
                    return;
                }

                SetSession(model.Id);
                if (model.Conversation != null && model.Conversation.Any())
                {
                    foreach (var entry in model.Conversation)
                    {
                        Messages.Add(new ConversationMessage
                        {
                            Speaker = entry.Speaker ?? string.Empty,
                            Text = entry.Message ?? string.Empty,
                            TimestampUtc = entry.TimestampUtc
                        });
                    }
                }
                else
                {
                    Messages.Add(new ConversationMessage
                    {
                        Speaker = "EngineOS",
                        Text = "I’m ready to answer questions about the loaded project and its recorded engineering context.",
                        TimestampUtc = DateTime.UtcNow
                    });
                }

                _initialized = model.Id != Guid.Empty;
                _initializationFailed = !_initialized;
                OnPropertyChanged(nameof(StatusText));
                Debug.WriteLine($"[ED-EP7] InitializeAsync #{_instanceId} session created: {SessionId}");
            }
            catch (Exception ex)
            {
                _initializationFailed = true;
                Debug.WriteLine($"[ED-EP7] InitializeAsync #{_instanceId} failed: {ex}");
                Messages.Add(new ConversationMessage
                {
                    Speaker = "Engineering Partner",
                    Text = "Hello — the Engineering Partner is currently unavailable. You can still type a question and send it to retry the connection.",
                    TimestampUtc = DateTime.UtcNow
                });
            }
            finally
            {
                IsInitializing = false;
                _operationGate.Release();
                Debug.WriteLine($"[ED-EP7] InitializeAsync #{_instanceId} complete");
            }
        }

        public async Task SendCurrentMessageAsync()
        {
            var text = (Draft ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text) || !await _operationGate.WaitAsync(0).ConfigureAwait(true))
            {
                Debug.WriteLine("[ED-EP5.1] Ignored empty or concurrent message send");
                return;
            }

            IsSending = true;
            try
            {
                Debug.WriteLine("[ED-EP5.1] User message sending: " + text);
                Messages.Add(new ConversationMessage { Speaker = "You", Text = text, TimestampUtc = DateTime.UtcNow });
                Draft = string.Empty;

                if (!SessionId.HasValue || SessionId.Value == Guid.Empty)
                {
                    Debug.WriteLine("[ED-EP5.1] Retrying session creation on send");
                    var model = await _partner.StartSessionAsync(string.Empty);
                    if (model is null || model.Id == Guid.Empty)
                    {
                        throw new InvalidOperationException("The Engineering Partner did not return a valid session.");
                    }

                    SetSession(model.Id);
                    _initialized = true;
                    _initializationFailed = false;
                }

                Debug.WriteLine("[ED-EP-READONLY] Calling structured/read-only partner response");
                StructuredConversationResult? structured = null;
                string reply;
                if (_structuredPartner is not null)
                {
                    structured = await _structuredPartner.SendStructuredReadOnlyMessageAsync(SessionId!.Value, text).ConfigureAwait(true);
                    reply = structured.Reply;
                }
                else
                {
                    reply = await _partner.SendReadOnlyMessageAsync(SessionId!.Value, text).ConfigureAwait(true);
                }

                SetPendingAction(structured);
                Debug.WriteLine("[ED-EP-READONLY] Partner response received");
                Messages.Add(new ConversationMessage
                {
                    Speaker = "EngineOS",
                    Text = string.IsNullOrWhiteSpace(reply) ? "(no reply was returned)" : reply,
                    TimestampUtc = DateTime.UtcNow,
                    ResponseKind = structured?.Kind ?? ConversationResponseKind.Informational,
                    ProposedAction = structured?.ProposedAction
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ED-EP-READONLY] Error during SendReadOnlyMessageAsync: " + ex);
                Messages.Add(new ConversationMessage
                {
                    Speaker = "EngineOS",
                    Text = "I couldn’t answer that because the conversation service is unavailable. Please try again.",
                    TimestampUtc = DateTime.UtcNow
                });
            }
            finally
            {
                IsSending = false;
                _operationGate.Release();
            }
            Debug.WriteLine("[ED-EP5.1] Conversation updated");
        }

        private void SetPendingAction(StructuredConversationResult? structured)
        {
            if (structured?.RequiresConfirmation == true)
            {
                PendingAction = structured.ProposedAction;
                _pendingOperation = structured.Operation;
                return;
            }

            PendingAction = null;
            _pendingOperation = null;
        }

        private async Task ConfirmPendingActionAsync()
        {
            var action = PendingAction;
            var operation = _pendingOperation;
            if (string.IsNullOrWhiteSpace(action) || IsConfirming) return;

            IsConfirming = true;
            try
            {
                if (operation is not (EngineeringOperationKind.Build or EngineeringOperationKind.Test))
                {
                    AddExecutionMessage(
                        $"I cannot execute {action} from Conversation yet. Only confirmed Build and Test operations are supported in this phase; nothing was run or changed.",
                        action);
                    return;
                }

                if (_operationExecutor is null)
                {
                    AddExecutionMessage(
                        $"{FormatOperation(operation.Value)} could not be executed because the development operation surface is unavailable. No result was recorded and nothing was changed.",
                        action);
                    return;
                }

                DevelopmentCommandResult? result = await _operationExecutor.ExecuteAsync(operation.Value).ConfigureAwait(true);
                AddExecutionMessage(FormatExecutionResult(operation.Value, result), action);
            }
            catch (Exception ex)
            {
                AddExecutionMessage(
                    $"{FormatOperation(operation ?? EngineeringOperationKind.Build)} could not be completed: {ex.Message}. See Output and Problems for details.",
                    action);
            }
            finally
            {
                PendingAction = null;
                _pendingOperation = null;
                IsConfirming = false;
            }
        }

        private void AddExecutionMessage(string text, string action)
        {
            Messages.Add(new ConversationMessage
            {
                Speaker = "EngineOS",
                Text = text,
                TimestampUtc = DateTime.UtcNow,
                ResponseKind = ConversationResponseKind.Informational,
                ProposedAction = action
            });
        }

        private static string FormatExecutionResult(EngineeringOperationKind operation, DevelopmentCommandResult? result)
        {
            var label = FormatOperation(operation);
            if (result is null)
            {
                return $"{label} could not be executed. No result was available; see Output and Problems for details.";
            }

            return result.Succeeded
                ? $"{label} completed successfully. See Output and Results for the captured evidence."
                : $"{label} failed. See Output and Problems for details.";
        }

        private static string FormatOperation(EngineeringOperationKind operation) => operation switch
        {
            EngineeringOperationKind.Build => "Build",
            EngineeringOperationKind.Test => "Tests",
            _ => operation.ToString()
        };
    }
}
