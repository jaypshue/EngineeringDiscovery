using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using EngineeringDiscovery.Core.Domain.EngineeringModel;

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class ProductDefinitionView : System.Windows.Controls.UserControl
    {
        private readonly Guid _modelId;
        private EngineeringQuestion? _currentQuestion;
        private readonly EngineeringDiscovery.Core.Services.IEnginerringConversationOrchestrator? _orchestrator;

        public ProductDefinitionView(Guid modelId)
        {
            InitializeComponent();
            _modelId = modelId;

            // Resolve orchestrator from the host service provider exposed on App
            _orchestrator = EngineeringDiscovery.Wpf.App.ServiceProvider?.GetService(typeof(EngineeringDiscovery.Core.Services.IEnginerringConversationOrchestrator)) as EngineeringDiscovery.Core.Services.IEnginerringConversationOrchestrator;

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            if (_orchestrator == null)
            {
                // No orchestrator available; show empty state
                IdeaTextBlock.Text = string.Empty;
                QuestionTextBlock.Text = "(orchestrator unavailable)";
                PreviousButton.IsEnabled = true;
                return;
            }

            var model = await _orchestrator.GetModelAsync(_modelId).ConfigureAwait(false);
            // Populate original idea
            _ = System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                IdeaTextBlock.Text = model?.OriginalIdea ?? string.Empty;
            }));

            await LoadNextQuestionAsync().ConfigureAwait(false);
        }

        private async Task LoadNextQuestionAsync()
        {
            if (_orchestrator == null) return;

            var next = await _orchestrator.GetNextQuestionAsync(_modelId).ConfigureAwait(false);
            _currentQuestion = next;

            await System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentQuestion != null)
                {
                    QuestionTextBlock.Text = _currentQuestion.Question;
                    AnswerTextBox.Text = string.Empty;
                    PreviousButton.IsEnabled = true; // allow returning to discovery
                }
                else
                {
                    QuestionTextBlock.Text = "(no more questions)";
                }
            }));
        }

        private async void ContinueButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_orchestrator == null) return;
            if (_currentQuestion == null) return;

            var answer = AnswerTextBox.Text ?? string.Empty;
            await _orchestrator.SubmitAnswerAsync(_modelId, _currentQuestion.Id, answer).ConfigureAwait(false);

            // Request next question
            var next = await _orchestrator.GetNextQuestionAsync(_modelId).ConfigureAwait(false);
            if (next != null)
            {
                _currentQuestion = next;
                await System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    QuestionTextBlock.Text = _currentQuestion.Question;
                    AnswerTextBox.Text = string.Empty;
                }));
            }
            else
            {
                // Discovery complete — navigate to ProductUnderstandingView using the model
                var model = await _orchestrator.GetModelAsync(_modelId).ConfigureAwait(false);
                var answers = model?.Conversation.Where(c => c.Speaker == "Engineer").Select(c => c.Message).ToList()
                              ?? new List<string>();

                var win = System.Windows.Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
                if (win != null)
                {
                    await System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Discovery completes: show ProductUnderstandingView, but do not route back into Product Discovery startup path
                        win.HostContent.Content = new ProductUnderstandingView(model?.OriginalIdea ?? string.Empty, answers);
                    }));
                }
            }
        }

        private void PreviousButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Return to initial discovery screen
            var win = System.Windows.Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
            if (win != null)
            {
                // Return to the central Engineering Workspace instead of ProductDiscoveryPlaceholder
                win.HostContent.Content = new EngineeringWorkspace();
            }
        }
    }
}
