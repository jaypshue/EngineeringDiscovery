using System.Windows;

namespace EngineeringDiscovery.Wpf.Views
{
    public partial class WelcomeView : System.Windows.Controls.UserControl
    {
        public WelcomeView()
        {
            InitializeComponent();
            // no-op change to force file update
            // Click handlers are wired in XAML; avoid double-subscription from code-behind
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
                    // Create workspace view with its owning view model so the view does not construct VMs in code-behind
                    var sp = EngineeringDiscovery.Wpf.App.ServiceProvider;
                    if (sp != null)
                    {
                        var partner = sp.GetService(typeof(EngineeringDiscovery.Core.Services.IEngineeringPartner)) as EngineeringDiscovery.Core.Services.IEngineeringPartner;
                        if (partner != null)
                        {
                            host.Content = new EngineeringWorkspace { DataContext = new EngineeringDiscovery.Wpf.ViewModels.EngineeringWorkspaceViewModel(partner) };
                            System.Diagnostics.Debug.WriteLine("[ED-315] Navigated to EngineeringWorkspace with VM");
                            return;
                        }
                    }
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
            System.Diagnostics.Debug.WriteLine("[ED-EP7] FreeRangeButton_Click invoked");
            var win = Window.GetWindow(this) as MainWindow ?? System.Windows.Application.Current?.MainWindow as MainWindow;
            if (win != null)
            {
                var host = win.FindName("HostContent") as System.Windows.Controls.ContentControl ?? win.HostContent as System.Windows.Controls.ContentControl;
                if (host != null)
                {
                    // ED-3: Replace startup Product Discovery navigation with the Engineering Workspace
                    host.Content = new EngineeringWorkspace();
                    System.Diagnostics.Debug.WriteLine("[ED-EP7] Navigated to EngineeringWorkspace (replacing ProductDiscoveryPlaceholder)");
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
