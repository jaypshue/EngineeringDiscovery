using System.Collections.Generic;

// Fully qualify WPF types below to avoid ambiguity with System.Windows.Forms types

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class ProductDefinitionView : System.Windows.Controls.UserControl
    {
        private readonly List<string> _questions = new()
        {
            "Who will use this product?",
            "What problem are they trying to solve?",
            "What outcome are they expecting?",
            "When will they use it?",
            "Why is the current approach insufficient?"
        };

        private int _currentIndex = 0;
        private string _idea = string.Empty;
        private readonly List<string> _answers;

        public ProductDefinitionView(string idea, List<string>? answers = null)
        {
            InitializeComponent();
            _idea = idea ?? string.Empty;
            IdeaTextBlock.Text = _idea;

            // Initialize answers list with provided values or empty placeholders
            if (answers != null && answers.Count == _questions.Count)
            {
                _answers = new List<string>(answers);
            }
            else
            {
                _answers = new List<string>(_questions.Count);
                for (int i = 0; i < _questions.Count; i++) _answers.Add(string.Empty);
            }

            ShowQuestion();
        }

        private void ShowQuestion()
        {
            if (_currentIndex < 0) _currentIndex = 0;
            if (_currentIndex >= _questions.Count) _currentIndex = _questions.Count - 1;
            QuestionTextBlock.Text = _questions[_currentIndex];
            // Restore any in-memory answer for this question
            AnswerTextBox.Text = _answers[_currentIndex] ?? string.Empty; // placeholder logic; in future answers will persist
            PreviousButton.IsEnabled = _currentIndex > 0;
        }

        private void ContinueButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Save current answer
            _answers[_currentIndex] = AnswerTextBox.Text ?? string.Empty;

            // Placeholder: advance to next question
            if (_currentIndex < _questions.Count - 1)
            {
                _currentIndex++;
                ShowQuestion();
            }
            else
            {
                // All questions answered — navigate to Product Understanding view
                var win = System.Windows.Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
                if (win != null)
                {
                    win.HostContent.Content = new ProductUnderstandingView(_idea, new List<string>(_answers));
                }
            }
        }

        private void PreviousButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                ShowQuestion();
            }
            else
            {
                // Navigate back to the initial ProductDiscoveryPlaceholder view
                var win = System.Windows.Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
                if (win != null)
                {
                    win.HostContent.Content = new ProductDiscoveryPlaceholder();
                }
            }
        }
    }
}
