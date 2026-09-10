using System;
using System.Windows;
using EngineeringDiscovery.Wpf.ViewModels;

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class WelcomeView : System.Windows.Controls.UserControl
    {
        public WelcomeView()
        {
            InitializeComponent();
        }

        private void OpenProjectButton_Click(object? sender, RoutedEventArgs e)
        {
            var mainVm = EngineeringDiscovery.Wpf.App.ServiceProvider?.GetService(typeof(MainWindowViewModel)) as MainWindowViewModel;
            if (mainVm is null)
            {
                System.Windows.MessageBox.Show("The project import workflow is unavailable.", "Open Project");
                return;
            }

            try
            {
                // MainWindow owns the state-driven transition to EngineeringWorkspace after durable import.
                mainVm.OpenRepositoryCommand.Execute(null);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Import failed: {ex.Message}", "Open Project");
            }
        }
    }
}
