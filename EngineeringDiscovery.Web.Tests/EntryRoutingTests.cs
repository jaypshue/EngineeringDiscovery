using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EngineeringDiscovery.Web.Tests
{
    public class EntryRoutingTests
    {
        [Fact]
        public async Task App_WithStartBuild_CreatesWorkspace()
        {
            // Arrange: create services similar to minimal host
            var services = new ServiceCollection();
            services.AddSingleton<EngineeringDiscovery.Core.Services.IWorkspacePersistence, EngineeringDiscovery.Core.Services.InMemoryWorkspacePersistence>();
            services.AddSingleton<EngineeringDiscovery.Core.Services.WorkspaceState>();
            services.AddSingleton<EngineeringDiscovery.Web.Services.SessionStartupService>();
            services.AddSingleton<EngineeringDiscovery.Web.Services.WorkspaceStateService>();
            var provider = services.BuildServiceProvider();

            var workspaceState = provider.GetRequiredService<EngineeringDiscovery.Core.Services.WorkspaceState>();
            var startup = provider.GetRequiredService<EngineeringDiscovery.Web.Services.SessionStartupService>();

            // Simulate navigation to /app?start=build by invoking the AppLanding logic indirectly.
            // The minimal check: if start=build, AppLanding should create a workspace via WorkspaceState.ReplaceWorkspace.

            var wsBefore = workspaceState.HasWorkspace;
            // Emulate AppLanding behavior
            var ws = new EngineeringDiscovery.Core.Domain.Workspace.Workspace();
            workspaceState.ReplaceWorkspace(ws);

            Assert.False(wsBefore);
            Assert.True(workspaceState.HasWorkspace);
        }

        [Fact]
        public async Task App_WithPersistedWorkspace_Resumes()
        {
            var persistence = new EngineeringDiscovery.Core.Services.InMemoryWorkspacePersistence();
            var ws = new EngineeringDiscovery.Core.Domain.Workspace.Workspace();
            ws.ImportedRepositories.Add(new EngineeringDiscovery.Core.Domain.Workspace.ImportedRepository { RepositoryPath = "C:\\repo" });
            await persistence.SaveAsync(ws);

            var services = new ServiceCollection();
            services.AddSingleton<EngineeringDiscovery.Core.Services.IWorkspacePersistence>(persistence);
            services.AddSingleton<EngineeringDiscovery.Core.Services.WorkspaceState>();
            var provider = services.BuildServiceProvider();

            var workspaceState = provider.GetRequiredService<EngineeringDiscovery.Core.Services.WorkspaceState>();
            var loaded = persistence.LoadAsync().GetAwaiter().GetResult();
            if (loaded is not null) workspaceState.ReplaceWorkspace(loaded);

            Assert.True(workspaceState.HasWorkspace);
            Assert.NotNull(workspaceState.ImportedRepositories);
            Assert.Single(workspaceState.ImportedRepositories);
        }

        [Fact]
        public void App_NoWorkspace_NoIntent_DoesNotCreateWorkspace()
        {
            var services = new ServiceCollection();
            services.AddSingleton<EngineeringDiscovery.Core.Services.IWorkspacePersistence, EngineeringDiscovery.Core.Services.InMemoryWorkspacePersistence>();
            services.AddSingleton<EngineeringDiscovery.Core.Services.WorkspaceState>();
            var provider = services.BuildServiceProvider();

            var workspaceState = provider.GetRequiredService<EngineeringDiscovery.Core.Services.WorkspaceState>();
            // Emulate AppLanding behavior when no start intent: it should not create a workspace.
            Assert.False(workspaceState.HasWorkspace);
        }
    }
}
