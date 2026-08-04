using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Hosting;

namespace EngineeringDiscovery.Wpf
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register WPF host services, ViewModels, and Core services
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.IWindowManager, EngineeringDiscovery.Wpf.Services.WindowManager>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.IDialogService, EngineeringDiscovery.Wpf.Services.DialogService>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IRepositoryProvider, EngineeringDiscovery.Core.Services.SampleDiscoveryEngine>();

                    // Register WorkspaceState from Core as the single source of truth (State Ownership rule: Core owns business state)
                    services.AddSingleton<EngineeringDiscovery.Core.Services.WorkspaceState>();

                    // Register WPF view-state store as singleton so host manages UI-only state
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IViewStateStore, EngineeringDiscovery.Wpf.Services.WpfViewStateStore>();

                    // ViewModels
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.MainWindowViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.WorkspaceHostViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.RepositoryExplorerViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.InspectorViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.OutputViewModel>();

                    // Main window
                    services.AddSingleton<MainWindow>();
                })
                .Build();

            await _host.StartAsync();

            var main = _host.Services.GetRequiredService<MainWindow>();
            main.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
            base.OnExit(e);
        }
    }
}
