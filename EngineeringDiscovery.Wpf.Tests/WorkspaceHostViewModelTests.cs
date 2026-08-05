using Xunit;
using Microsoft.Extensions.DependencyInjection;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.ViewModels;
using EngineeringDiscovery.Wpf.Services;

namespace EngineeringDiscovery.Wpf.Tests;

public class WorkspaceHostViewModelTests
{
    [Fact]
    public void WorkspaceHostViewModel_Constructs_With_WorkspaceState()
    {
        var services = new ServiceCollection();
        // Register test-friendly core dependencies
        services.AddSingleton<IWorkspacePersistence, InMemoryWorkspacePersistence>();
        services.AddSingleton<IRepoFingerprintService, TestRepoFingerprintService>();
        services.AddSingleton<WorkspaceState>();

        // Register WPF services used by the view model
        services.AddSingleton<RepositorySelectionService>();

        services.AddTransient<WorkspaceHostViewModel>();
        var sp = services.BuildServiceProvider();

        var vm = sp.GetRequiredService<WorkspaceHostViewModel>();

        Assert.NotNull(vm);
        // RepositoryPath may be null for empty workspace; just ensure property accessor doesn't throw
        _ = vm.RepositoryPath;
    }
}
