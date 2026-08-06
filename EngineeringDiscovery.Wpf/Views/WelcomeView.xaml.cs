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
            System.Diagnostics.Debug.WriteLine("[ED-315] CorporateButton_Click invoked");
            var win = Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
            if (win != null)
            {
                var host = win.FindName("HostContent") as System.Windows.Controls.ContentControl ?? win.HostContent as System.Windows.Controls.ContentControl;
                if (host != null)
                {
                    host.Content = new EngineeringWorkspace();
                    System.Diagnostics.Debug.WriteLine("[ED-315] Navigated to EngineeringWorkspace");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ED-315] HostContent not found on MainWindow");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ED-315] MainWindow not found from WelcomeView");
            }
        }

        private void FreeRangeButton_Click(object? sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[ED-315] FreeRangeButton_Click invoked");
            var win = Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
            if (win != null)
            {
                var host = win.FindName("HostContent") as System.Windows.Controls.ContentControl ?? win.HostContent as System.Windows.Controls.ContentControl;
                if (host != null)
                {
                    // ED-3: Replace startup Product Discovery navigation with the Engineering Workspace
                    host.Content = new EngineeringWorkspace();
                    System.Diagnostics.Debug.WriteLine("[ED-315] Navigated to EngineeringWorkspace (replacing ProductDiscoveryPlaceholder)");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ED-315] HostContent not found on MainWindow");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ED-315] MainWindow not found from WelcomeView");
            }
        }
    }
}
