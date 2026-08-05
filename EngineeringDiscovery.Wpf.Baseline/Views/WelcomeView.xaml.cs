using System.Windows;
using System.Windows.Controls;

namespace EngineeringDiscovery.Wpf.Baseline.Views
{
    public partial class WelcomeView : UserControl
    {
        public WelcomeView()
        {
            InitializeComponent();
            CorporateButton.Click += CorporateButton_Click;
            FreeRangeButton.Click += FreeRangeButton_Click;
        }

        private void CorporateButton_Click(object? sender, RoutedEventArgs e)
        {
            var win = Window.GetWindow(this) as MainWindow;
            if (win != null)
            {
                // swap in the EngineeringWorkspace user control
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
