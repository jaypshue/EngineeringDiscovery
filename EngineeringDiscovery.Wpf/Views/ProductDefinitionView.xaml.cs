using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
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

            await LoadNextResponseAsync().ConfigureAwait(false);
        }

        private async Task LoadNextResponseAsync()
        {
            if (_orchestrator == null) return;

            var next = await _orchestrator.RespondAsync(_modelId).ConfigureAwait(false);
            _currentQuestion = next;
            var response = next?.Question ?? string.Empty;

            await System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_currentQuestion != null && !string.IsNullOrWhiteSpace(response))
                {
                    QuestionTextBlock.Text = response;
                    AnswerTextBox.Text = string.Empty;
                    PreviousButton.IsEnabled = true; // allow returning to discovery
                }
                else
                {
                    QuestionTextBlock.Text = "(no more responses)";
                }
            }));
        }

        private async void ContinueButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_orchestrator == null) return;
            if (_currentQuestion == null) return;

            var answer = AnswerTextBox.Text ?? string.Empty;
            await _orchestrator.SubmitAnswerAsync(_modelId, _currentQuestion.Id, answer).ConfigureAwait(false);
            await LoadNextResponseAsync().ConfigureAwait(false);
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
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
