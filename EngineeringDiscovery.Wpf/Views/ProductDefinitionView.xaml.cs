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

        public ProductDefinitionView(string idea)
        {
            InitializeComponent();
            _idea = idea ?? string.Empty;
            IdeaTextBlock.Text = _idea;
            ShowQuestion();
        }

        private void ShowQuestion()
        {
            if (_currentIndex < 0) _currentIndex = 0;
            if (_currentIndex >= _questions.Count) _currentIndex = _questions.Count - 1;
            QuestionTextBlock.Text = _questions[_currentIndex];
            AnswerTextBox.Text = string.Empty; // placeholder logic; in future answers will persist
            PreviousButton.IsEnabled = _currentIndex > 0;
        }

        private void ContinueButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Placeholder: advance to next question
            if (_currentIndex < _questions.Count - 1)
            {
                _currentIndex++;
                ShowQuestion();
            }
            else
            {
                // All questions answered — navigate to workspace placeholder
                var win = System.Windows.Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
                if (win != null)
                {
                    win.HostContent.Content = new EngineeringWorkspace();
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
