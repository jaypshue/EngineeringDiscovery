using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace EngineeringDiscovery.Wpf
{
    public partial class App : System.Windows.Application
    {
        public App()
        {
            // Ensure Application XAML resources (ResourceDictionary, DataTemplates) are initialized
            InitializeComponent();
        }

        private IHost? _host;
        public static IServiceProvider? ServiceProvider { get; private set; }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Register WPF host services, ViewModels, and Core services
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.IWindowManager, EngineeringDiscovery.Wpf.Services.WindowManager>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.WindowPlacementService>(_ =>
                        new EngineeringDiscovery.Wpf.Services.WindowPlacementService(
                            System.IO.Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "EngineeringDiscovery",
                                "window-placement.json"),
                            new EngineeringDiscovery.Wpf.Services.ScreenWorkAreaProvider()));
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.IDialogService, EngineeringDiscovery.Wpf.Services.DialogService>();
                    // Repository providers are web-specific; WPF uses the InvestigationEngine directly.
                    // Keep registration minimal here; the WorkspaceHostViewModel will construct the Investigation via WPF services.

                    // Register WorkspaceState from Core as the single source of truth (State Ownership rule: Core owns business state)
                    // Register persistence and WorkspaceState. WorkspaceState must not perform I/O in its constructor;
                    // the host is responsible for loading persisted state and calling ReplaceWorkspace.
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IWorkspacePersistence, EngineeringDiscovery.Core.Services.FileWorkspacePersistence>(sp =>
                        new EngineeringDiscovery.Core.Services.FileWorkspacePersistence(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EngineeringDiscovery")));
                    services.AddSingleton<EngineeringDiscovery.Core.Services.WorkspaceState>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IProjectStateService, EngineeringDiscovery.Core.Services.ProjectStateService>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringStateQuery, EngineeringDiscovery.Core.Services.EngineeringStateQuery>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringIterationService, EngineeringDiscovery.Core.Services.EngineeringIterationService>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.IGitStatusService, EngineeringDiscovery.Wpf.Services.GitStatusService>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.IRepositoryFileService, EngineeringDiscovery.Wpf.Services.RepositoryFileService>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.IGitChangesService, EngineeringDiscovery.Wpf.Services.GitChangesService>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.IDevelopmentCommandService, EngineeringDiscovery.Wpf.Services.DevelopmentCommandService>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.ICodingAgentProcessRunner, EngineeringDiscovery.Core.Services.SystemCodingAgentProcessRunner>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.ICodingAgentProvider, EngineeringDiscovery.Core.Services.CopilotCodingAgentProvider>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.ICodingAgentHandoffService, EngineeringDiscovery.Core.Services.CodingAgentHandoffService>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringOperationGateway, EngineeringDiscovery.Wpf.Services.WpfEngineeringOperationGateway>();

                    // Production repo fingerprint service
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IRepoFingerprintService, EngineeringDiscovery.Core.Services.FileRepoFingerprintService>();

                    // Register WPF view-state store as singleton so host manages UI-only state
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IViewStateStore, EngineeringDiscovery.Wpf.Services.WpfViewStateStore>();

                    // ViewModels
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.MainWindowViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.ActivityViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.WorkspaceHostViewModel>();
                    services.AddTransient<EngineeringDiscovery.Wpf.ViewModels.DevelopmentSurfaceViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.RepositoryExplorerViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.InspectorViewModel>();
                    services.AddSingleton<EngineeringDiscovery.Wpf.ViewModels.OutputViewModel>();

                    // Interaction services (WPF) - Repository selection / startup flow
                    services.AddSingleton<EngineeringDiscovery.Wpf.Services.RepositorySelectionService>();

                    // Main window
                    services.AddSingleton<MainWindow>();

                    // Register EngineeringModel repository in Core via interface
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringModelRepository, EngineeringDiscovery.Core.Services.InMemoryEngineeringModelRepository>();

                    // Register EnginerringConversationOrchestrator
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IEnginerringConversationOrchestrator, EngineeringDiscovery.Core.Services.EnginerringConversationOrchestrator>();

                    // Register EngineeringPartner so WPF views can resolve the conversation session owner
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringConversationCapabilityService, EngineeringDiscovery.Core.Services.EngineeringConversationCapabilityService>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringPartner, EngineeringDiscovery.Core.Services.EngineeringPartner>();

                    // Register AI-backed conversation service and its HttpClient
                    services.AddHttpClient<EngineeringDiscovery.Core.Services.OpenAIEngineeringConversationService>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IEngineeringConversationService, EngineeringDiscovery.Core.Services.OpenAIEngineeringConversationService>(sp =>
                        sp.GetRequiredService<EngineeringDiscovery.Core.Services.OpenAIEngineeringConversationService>());
                })
                .Build();

            // Expose service provider for views that are constructed manually
            ServiceProvider = _host.Services;

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
                    else
                    {
                        // No project is open: keep the authoritative state empty so the launcher is shown.
                        workspaceState.ReplaceWorkspace(new EngineeringDiscovery.Core.Domain.Workspace.Workspace());
                    }
            }

            var main = _host.Services.GetRequiredService<MainWindow>();
            main.Show();

            // Schedule EngineOS presentation evidence collection once the UI is idle.
            // This is the first Evidence Collector (presentation-layer only).
            try
            {
                _ = main.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        await EngineeringDiscovery.Wpf.Services.EngineOSEvidenceCollector.CollectAsync(main).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"EngineOS EvidenceCollector error: {ex}");
                    }
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to schedule EvidenceCollector: {ex}");
            }
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
