using System.Collections.Generic;

// Fully qualify WPF types to avoid ambiguity with WinForms

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class ProductUnderstandingView : System.Windows.Controls.UserControl
    {
        private readonly string _idea;
        private readonly List<string> _answers;

        public ProductUnderstandingView(string idea, List<string> answers)
        {
            InitializeComponent();
            _idea = idea ?? string.Empty;
            _answers = answers ?? new List<string>();

            IdeaTextBlock.Text = _idea;

            // Map answers to the placeholder sections
            TargetUsersText.Text = _answers.Count > 0 ? _answers[0] : "(not specified)";
            var understanding = "";
            if (_answers.Count > 1) understanding += _answers[1] + "\n";
            if (_answers.Count > 2) understanding += _answers[2] + "\n";
            if (string.IsNullOrWhiteSpace(understanding)) understanding = "(no summary available)";
            CurrentUnderstandingText.Text = understanding;
        }

        private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Go back to ProductDefinitionView with existing answers
            var win = System.Windows.Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
            if (win != null)
            {
                // Navigate back to ProductDefinitionView using the existing model if possible.
                var sp = EngineeringDiscovery.Wpf.App.ServiceProvider;
                var repo = sp?.GetService(typeof(EngineeringDiscovery.Core.Services.IEngineeringModelRepository)) as EngineeringDiscovery.Core.Services.IEngineeringModelRepository;
                if (repo != null)
                {
                    // Try to find a model with matching original idea
                    var allModel = repo.GetAsync(Guid.Empty).GetAwaiter().GetResult();
                    // If cannot locate by original idea, fall back to opening discovery placeholder
                    // After product understanding navigation, return to Welcome/Workspace rather than Product Discovery
                    win.HostContent.Content = new EngineeringWorkspace();
                }
                else
                {
                    // After product understanding navigation, return to Welcome/Workspace rather than Product Discovery
                    win.HostContent.Content = new EngineeringWorkspace();
                }
            }
        }

        private void YesButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Placeholder: user confirms understanding; continue to workspace placeholder
            var win = System.Windows.Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
            if (win != null)
            {
                win.HostContent.Content = new EngineeringWorkspace();
            }
        }

        private void ContinueButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Continue behaves same as Yes for now
            YesButton_Click(sender, e);
        }
    }
}
