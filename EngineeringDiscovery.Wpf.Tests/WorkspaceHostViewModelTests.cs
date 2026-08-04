using Xunit;
using Microsoft.Extensions.DependencyInjection;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.ViewModels;

namespace EngineeringDiscovery.Wpf.Tests;

public class WorkspaceHostViewModelTests
{
    [Fact]
    public void WorkspaceHostViewModel_Constructs_With_WorkspaceState()
    {
        var services = new ServiceCollection();
        services.AddSingleton<WorkspaceState>();
        services.AddTransient<WorkspaceHostViewModel>();
        var sp = services.BuildServiceProvider();

        var vm = sp.GetRequiredService<WorkspaceHostViewModel>();

        Assert.NotNull(vm);
        Assert.NotNull(vm.RepositoryPath);
    }
}
