using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Models;
using EngineeringDiscovery.Wpf.Services;
using EngineeringDiscovery.Wpf.ViewModels;

namespace EngineeringDiscovery.Wpf.Tests;

public sealed class DevelopmentSurfaceViewModelTests
{
    [Fact]
    public async Task SearchCommands_RequireRepositoryAndQuery_ThenPopulateTheRequestedResults()
    {
        var root = CreateTempDirectory();
        try
        {
            var query = new Mock<IEngineeringStateQuery>();
            query.Setup(item => item.GetWorkspaceContext()).Returns(new WorkspaceContext
            {
                HasRepository = true,
                RepositoryName = "SearchFixture",
                RepositoryPath = root
            });

            var fileService = new Mock<IRepositoryFileService>();
            fileService.Setup(service => service.GetTreeAsync(root, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RepositoryFileNode("SearchFixture", root, string.Empty, true));
            fileService.Setup(service => service.SearchFileNamesAsync(root, "Feature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new FileSearchResult("src/Feature.cs", Path.Combine(root, "src", "Feature.cs"), null, null, null)
                });
            fileService.Setup(service => service.SearchTextAsync(root, "Feature", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new FileSearchResult("src/Feature.cs", Path.Combine(root, "src", "Feature.cs"), 1, 14, "public class Feature")
                });

            var gitChanges = new Mock<IGitChangesService>();
            gitChanges.Setup(service => service.GetChangesAsync(root, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitChange>());

            using var vm = new DevelopmentSurfaceViewModel(
                query.Object,
                new WorkspaceState(new InMemoryWorkspacePersistence(), new TestRepoFingerprintService()),
                fileService.Object,
                gitChanges.Object,
                new Mock<IDevelopmentCommandService>().Object);

            var fileSearchCommand = Assert.IsType<CommunityToolkit.Mvvm.Input.AsyncRelayCommand>(vm.SearchFilesCommand);
            var textSearchCommand = Assert.IsType<CommunityToolkit.Mvvm.Input.AsyncRelayCommand>(vm.SearchTextCommand);
            Assert.False(fileSearchCommand.CanExecute(null));
            Assert.False(textSearchCommand.CanExecute(null));

            await vm.InitializeAsync();
            Assert.False(fileSearchCommand.CanExecute(null));
            Assert.False(textSearchCommand.CanExecute(null));

            vm.SearchQuery = "Feature";
            Assert.True(fileSearchCommand.CanExecute(null));
            Assert.True(textSearchCommand.CanExecute(null));

            await fileSearchCommand.ExecuteAsync(null);
            Assert.Single(vm.SearchResults);
            Assert.Equal("1 file name result(s).", vm.StatusText);
            fileService.Verify(service => service.SearchFileNamesAsync(root, "Feature", It.IsAny<CancellationToken>()), Times.Once);

            await textSearchCommand.ExecuteAsync(null);
            Assert.Single(vm.TextSearchResults);
            Assert.Equal("1 source text result(s).", vm.StatusText);
            fileService.Verify(service => service.SearchTextAsync(root, "Feature", It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public void SelectedWorkbenchView_DefaultsToFiles_AndCanSwitchBetweenExistingViews()
    {
        using var vm = new DevelopmentSurfaceViewModel(
            new Mock<IEngineeringStateQuery>().Object,
            new WorkspaceState(new InMemoryWorkspacePersistence(), new TestRepoFingerprintService()),
            new Mock<IRepositoryFileService>().Object,
            new Mock<IGitChangesService>().Object,
            new Mock<IDevelopmentCommandService>().Object);

        Assert.Equal(DevelopmentWorkbenchView.Files, vm.SelectedWorkbenchView);

        vm.SelectedWorkbenchView = DevelopmentWorkbenchView.Search;
        Assert.Equal(DevelopmentWorkbenchView.Search, vm.SelectedWorkbenchView);

        vm.SelectedWorkbenchView = DevelopmentWorkbenchView.Results;
        Assert.Equal(DevelopmentWorkbenchView.Results, vm.SelectedWorkbenchView);
    }

    [Fact]
    public async Task ConfirmedBuildAndTest_PreserveOutputProblemsResultsAndStatusEvidence()
    {
        var root = CreateTempDirectory();
        try
        {
            var query = new Mock<IEngineeringStateQuery>();
            query.Setup(item => item.GetWorkspaceContext()).Returns(new WorkspaceContext
            {
                HasRepository = true,
                RepositoryName = "EvidenceFixture",
                RepositoryPath = root
            });

            var fileService = new Mock<IRepositoryFileService>();
            fileService.Setup(service => service.GetTreeAsync(root, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RepositoryFileNode("EvidenceFixture", root, string.Empty, true));
            var gitChanges = new Mock<IGitChangesService>();
            gitChanges.Setup(service => service.GetChangesAsync(root, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<GitChange>());

            var buildResult = new DevelopmentCommandResult(
                DevelopmentCommandKind.Build,
                "dotnet build",
                0,
                TimeSpan.FromSeconds(1),
                Array.Empty<ProblemItem>(),
                "build output");
            var testProblem = new ProblemItem(ProblemSeverity.Error, "Test failure", Source: "dotnet test");
            var testResult = new DevelopmentCommandResult(
                DevelopmentCommandKind.Test,
                "dotnet test",
                1,
                TimeSpan.FromSeconds(2),
                new[] { testProblem },
                "test output");
            var commandService = new Mock<IDevelopmentCommandService>();
            commandService.Setup(service => service.RunAsync(
                    DevelopmentCommandKind.Build,
                    root,
                    It.IsAny<Action<DevelopmentOutputEntry>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<DevelopmentCommandKind, string, Action<DevelopmentOutputEntry>, CancellationToken>(
                    (_, _, output, _) => output(new DevelopmentOutputEntry(DateTimeOffset.UtcNow, DevelopmentOutputChannel.Build, "build stream")))
                .ReturnsAsync(buildResult);
            commandService.Setup(service => service.RunAsync(
                    DevelopmentCommandKind.Test,
                    root,
                    It.IsAny<Action<DevelopmentOutputEntry>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<DevelopmentCommandKind, string, Action<DevelopmentOutputEntry>, CancellationToken>(
                    (_, _, output, _) => output(new DevelopmentOutputEntry(DateTimeOffset.UtcNow, DevelopmentOutputChannel.Test, "test stream")))
                .ReturnsAsync(testResult);

            using var vm = new DevelopmentSurfaceViewModel(
                query.Object,
                new WorkspaceState(new InMemoryWorkspacePersistence(), new TestRepoFingerprintService()),
                fileService.Object,
                gitChanges.Object,
                commandService.Object);
            await vm.InitializeAsync();

            var build = await vm.ExecuteAsync(EngineeringOperationKind.Build);
            Assert.Same(buildResult, build);
            Assert.Contains(vm.Output, entry => entry.Text == "build stream");
            Assert.Single(vm.Results);
            Assert.Equal("Build", vm.Results[0].Name);
            Assert.Equal("Build passed.", vm.StatusText);

            var test = await vm.ExecuteAsync(EngineeringOperationKind.Test);
            Assert.Same(testResult, test);
            Assert.Contains(vm.Output, entry => entry.Text == "test stream");
            Assert.Single(vm.Problems);
            Assert.Equal(testProblem.Message, vm.Problems[0].Message);
            Assert.Equal(2, vm.Results.Count);
            Assert.Equal("Tests", vm.Results[0].Name);
            Assert.Equal("Build", vm.Results[1].Name);
            Assert.Equal("Tests failed.", vm.StatusText);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "EngineOS-DevelopmentSurface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }
}
