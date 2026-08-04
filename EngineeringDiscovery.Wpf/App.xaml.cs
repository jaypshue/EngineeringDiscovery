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
                    // Register persistence and WorkspaceState. WorkspaceState must not perform I/O in its constructor;
                    // the host is responsible for loading persisted state and calling ReplaceWorkspace.
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IWorkspacePersistence, EngineeringDiscovery.Core.Services.FileWorkspacePersistence>(sp =>
                        new EngineeringDiscovery.Core.Services.FileWorkspacePersistence(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EngineeringDiscovery")));
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

            // After host starts, explicitly load persisted workspace and initialize WorkspaceState.
            using (var scope = _host.Services.CreateScope())
            {
                var persistence = scope.ServiceProvider.GetRequiredService<EngineeringDiscovery.Core.Services.IWorkspacePersistence>();
                var workspaceState = scope.ServiceProvider.GetRequiredService<EngineeringDiscovery.Core.Services.WorkspaceState>();
                var loaded = persistence.LoadAsync().GetAwaiter().GetResult();
                if (loaded is not null)
                {
                    workspaceState.ReplaceWorkspace(loaded);
                }
            }

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
