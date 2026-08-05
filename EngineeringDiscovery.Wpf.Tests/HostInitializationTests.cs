using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.ViewModels;
using EngineeringDiscovery.Wpf.Services;

namespace EngineeringDiscovery.Wpf.Tests
{
    public class HostInitializationTests
    {
        [Fact]
        public void Host_Registers_Core_Services_And_ViewModels()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Test-friendly persistence and fingerprint services
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IWorkspacePersistence, EngineeringDiscovery.Core.Services.InMemoryWorkspacePersistence>();
                    services.AddSingleton<EngineeringDiscovery.Core.Services.IRepoFingerprintService, EngineeringDiscovery.Core.Services.TestRepoFingerprintService>();

                    services.AddSingleton<WorkspaceState>();
                    // WPF-specific services required by view models
                    services.AddSingleton<RepositorySelectionService>();
                    services.AddSingleton<ActivityViewModel>();

                    services.AddSingleton<IWindowManager, WindowManager>();
                    services.AddSingleton<IDialogService, DialogService>();

                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<WorkspaceHostViewModel>();
                    services.AddSingleton<RepositoryExplorerViewModel>();
                    services.AddSingleton<InspectorViewModel>();
                    services.AddSingleton<OutputViewModel>();
                })
                .Build();

            var sp = host.Services;

            // Core state
            var ws = sp.GetService<WorkspaceState>();
            Assert.NotNull(ws);

            // Services
            var wm = sp.GetService<IWindowManager>();
            var ds = sp.GetService<IDialogService>();
            Assert.NotNull(wm);
            Assert.NotNull(ds);

            // ViewModels
            Assert.NotNull(sp.GetService<MainWindowViewModel>());
            Assert.NotNull(sp.GetService<WorkspaceHostViewModel>());
            Assert.NotNull(sp.GetService<RepositoryExplorerViewModel>());
            Assert.NotNull(sp.GetService<InspectorViewModel>());
            Assert.NotNull(sp.GetService<OutputViewModel>());
        }
    }
}
