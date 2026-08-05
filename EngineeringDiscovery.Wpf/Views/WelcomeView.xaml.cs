using System.Windows;

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class WelcomeView : System.Windows.Controls.UserControl
    {
        public WelcomeView()
        {
            InitializeComponent();
            // no-op change to force file update
            CorporateButton.Click += CorporateButton_Click;
            FreeRangeButton.Click += FreeRangeButton_Click;
        }

        private void CorporateButton_Click(object? sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this) as MainWindow;
            if (win != null)
            {
                win.HostContent.Content = new EngineeringWorkspace();
            }
        }

        private void FreeRangeButton_Click(object? sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this) as MainWindow;
            if (win != null)
            {
                win.HostContent.Content = new ProductDiscoveryPlaceholder();
            }
        }
    }
}
