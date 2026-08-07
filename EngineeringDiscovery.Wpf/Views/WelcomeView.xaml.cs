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
                    // Route through MainWindowViewModel's OpenRepositoryCommand so the normal repository selection/import
                    // workflow runs. After import completes and WorkspaceState has a workspace, navigate to the EngineeringWorkspace.
                    var sp = EngineeringDiscovery.Wpf.App.ServiceProvider;
                    var mainVm = sp?.GetService(typeof(EngineeringDiscovery.Wpf.ViewModels.MainWindowViewModel)) as EngineeringDiscovery.Wpf.ViewModels.MainWindowViewModel;
                    var partner = sp?.GetService(typeof(EngineeringDiscovery.Core.Services.IEngineeringPartner)) as EngineeringDiscovery.Core.Services.IEngineeringPartner;
                    var ws = sp?.GetService(typeof(EngineeringDiscovery.Core.Services.WorkspaceState)) as EngineeringDiscovery.Core.Services.WorkspaceState;

                    if (ws != null && mainVm != null)
                    {
                        void OnChange()
                        {
                            try
                            {
                                if (ws.HasWorkspace)
                                {
                                    // Navigate to EngineeringWorkspace with VM on UI thread
                                    if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                                    {
                                        host.Content = new EngineeringWorkspace { DataContext = new EngineeringDiscovery.Wpf.ViewModels.EngineeringWorkspaceViewModel(partner) };
                                    }
                                    else
                                    {
                                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => host.Content = new EngineeringWorkspace { DataContext = new EngineeringDiscovery.Wpf.ViewModels.EngineeringWorkspaceViewModel(partner) }));
                                    }
                                    ws.OnChange -= OnChange;
                                }
                            }
                            catch { ws.OnChange -= OnChange; }
                        }

                        ws.OnChange += OnChange;

                        // Trigger the OpenRepository flow (this shows folder picker and performs import)
                        try { mainVm.OpenRepositoryCommand.Execute(null); }
                        catch { /* ignore failures here; workspace state handler above will unsubscribe */ }
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine("[ED-315] Host or services not available to perform OpenRepository");
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
                    // Same flow as Corporate: trigger OpenRepository and navigate on successful import
                    var sp = EngineeringDiscovery.Wpf.App.ServiceProvider;
                    var mainVm = sp?.GetService(typeof(EngineeringDiscovery.Wpf.ViewModels.MainWindowViewModel)) as EngineeringDiscovery.Wpf.ViewModels.MainWindowViewModel;
                    var partner = sp?.GetService(typeof(EngineeringDiscovery.Core.Services.IEngineeringPartner)) as EngineeringDiscovery.Core.Services.IEngineeringPartner;
                    var ws = sp?.GetService(typeof(EngineeringDiscovery.Core.Services.WorkspaceState)) as EngineeringDiscovery.Core.Services.WorkspaceState;

                    if (ws != null && mainVm != null)
                    {
                        void OnChange()
                        {
                            try
                            {
                                if (ws.HasWorkspace)
                                {
                                    if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
                                    {
                                        host.Content = new EngineeringWorkspace { DataContext = new EngineeringDiscovery.Wpf.ViewModels.EngineeringWorkspaceViewModel(partner) };
                                    }
                                    else
                                    {
                                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => host.Content = new EngineeringWorkspace { DataContext = new EngineeringDiscovery.Wpf.ViewModels.EngineeringWorkspaceViewModel(partner) }));
                                    }
                                    ws.OnChange -= OnChange;
                                }
                            }
                            catch { ws.OnChange -= OnChange; }
                        }

                        ws.OnChange += OnChange;
                        try { mainVm.OpenRepositoryCommand.Execute(null); }
                        catch { /* ignore */ }
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine("[ED-EP7] Host or services not available to perform OpenRepository");
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
