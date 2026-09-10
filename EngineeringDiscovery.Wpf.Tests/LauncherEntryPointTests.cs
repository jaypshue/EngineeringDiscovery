using System;
using System.IO;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Services;
using EngineeringDiscovery.Wpf.ViewModels;
using Xunit;

namespace EngineeringDiscovery.Wpf.Tests;

public sealed class LauncherEntryPointTests
{
    [Fact]
    public void Launcher_Uses_Only_Open_Project_And_State_Driven_Workspace_Navigation()
    {
        var welcome = ReadWpfFile(Path.Combine("Views", "WelcomeView.xaml"));
        var welcomeCode = ReadWpfFile(Path.Combine("Views", "WelcomeView.xaml.cs"));
        var mainWindow = ReadWpfFile("MainWindow.xaml.cs");
        var mainWindowXaml = ReadWpfFile("MainWindow.xaml");

        Assert.Contains("Open Project", welcome, StringComparison.Ordinal);
        Assert.DoesNotContain("Free Range Engineering", welcome, StringComparison.Ordinal);
        Assert.DoesNotContain("Start Building", welcome, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductDiscoveryPlaceholder", welcomeCode, StringComparison.Ordinal);
        Assert.DoesNotContain("<ToolBar", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("<MenuItem Header=\"Open Project\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("<MenuItem Header=\"Close Project\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("if (_viewModel.HasActiveProject)", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ShowEngineeringWorkspace();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("ShowLauncher();", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void Launcher_Exposes_Intentional_NonBlocking_Project_Loading()
    {
        var welcome = ReadWpfFile(Path.Combine("Views", "WelcomeView.xaml"));
        var viewModel = ReadWpfFile(Path.Combine("ViewModels", "MainWindowViewModel.cs"));

        Assert.Contains("Binding IsLoading", welcome, StringComparison.Ordinal);
        Assert.Contains("ProgressBar", welcome, StringComparison.Ordinal);
        Assert.Contains("Binding LoadingStatusText", welcome, StringComparison.Ordinal);
        Assert.Contains("Opening project…", viewModel, StringComparison.Ordinal);
        Assert.Contains("Inspecting repository…", viewModel, StringComparison.Ordinal);
        Assert.Contains("Loading engineering context…", viewModel, StringComparison.Ordinal);
        Assert.Contains("finally", viewModel, StringComparison.Ordinal);
        Assert.Contains("IsLoading = false", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Open_Project_Is_Disabled_During_Import_And_Loading_Clears_On_Success()
    {
        var repositoryPath = CreateSupportedRepository();
        try
        {
            var persistence = new InMemoryWorkspacePersistence();
            var workspaceState = new WorkspaceState(persistence, new TestRepoFingerprintService());
            using var selection = new RepositorySelectionService(workspaceState, persistence);
            await selection.SelectPathAsync(repositoryPath);
            await WaitForDetectionAsync(selection);

            using var mainViewModel = new MainWindowViewModel(
                workspaceState,
                selection,
                new ActivityViewModel(workspaceState),
                persistence);

            var importTask = mainViewModel.CompleteSelectedRepositoryImportAsync();
            Assert.True(mainViewModel.IsLoading);
            Assert.False(mainViewModel.OpenRepositoryCommand.CanExecute(null));

            Assert.True(await importTask);
            Assert.False(mainViewModel.IsLoading);
            Assert.True(mainViewModel.OpenRepositoryCommand.CanExecute(null));
            Assert.True(mainViewModel.HasActiveProject);
        }
        finally
        {
            Directory.Delete(repositoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_Project_Load_Clears_Loading_And_Preserves_Launcher_State()
    {
        var repositoryPath = CreateSupportedRepository();
        try
        {
            var persistence = new FailingWorkspacePersistence { FailSaves = true };
            var workspaceState = new WorkspaceState(persistence, new TestRepoFingerprintService());
            using var selection = new RepositorySelectionService(workspaceState, persistence);
            await selection.SelectPathAsync(repositoryPath);
            await WaitForDetectionAsync(selection);

            using var mainViewModel = new MainWindowViewModel(
                workspaceState,
                selection,
                new ActivityViewModel(workspaceState),
                persistence);

            var loaded = await mainViewModel.CompleteSelectedRepositoryImportAsync(showErrors: false);

            Assert.False(loaded);
            Assert.False(mainViewModel.IsLoading);
            Assert.True(mainViewModel.OpenRepositoryCommand.CanExecute(null));
            Assert.False(mainViewModel.HasActiveProject);
            Assert.Null(workspaceState.ActiveWorkspace);
        }
        finally
        {
            Directory.Delete(repositoryPath, recursive: true);
        }
    }

    [Fact]
    public void Successful_Load_Still_Uses_Direct_EngineeringWorkspace_Route()
    {
        var mainWindow = ReadWpfFile("MainWindow.xaml.cs");

        Assert.Contains("ShowEngineeringWorkspace();", mainWindow, StringComparison.Ordinal);
        Assert.Contains("new Views.EngineeringWorkspace", mainWindow, StringComparison.Ordinal);
        Assert.Contains("DataContext = new EngineeringWorkspaceViewModel(partner)", mainWindow, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductDiscovery", mainWindow, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_Project_Banner_Uses_Authoritative_Project_Name()
    {
        var xaml = ReadEngineeringWorkspaceXaml();

        Assert.Contains("Text=\"{Binding RepositoryName, FallbackValue=Engineering Workspace}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding RepositoryPath}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ENGINEOS / PROJECT HEADER", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("EngineeringStatePanel.ProjectNameDisplay", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void EngineeringWorkspace_Uses_ReadOnly_Project_State_And_Steering()
    {
        var xaml = ReadEngineeringWorkspaceXaml();

        Assert.Contains("Text=\"{Binding EngineeringState.CurrentReadiness}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("DataContext=\"{Binding DevelopmentSurface.Steering}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<views:EngineeringSteeringPanel Grid.Column=\"2\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{StaticResource BackgroundBrush}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TextBox Text=\"{Binding EngineeringState.CurrentRecommendation}", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"Generate Implementation Plan\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Successful_Selected_Repository_Import_Persists_Project_And_Raises_Navigation_Boundary()
    {
        var repositoryPath = CreateSupportedRepository();
        try
        {
            var persistence = new InMemoryWorkspacePersistence();
            var workspaceState = new WorkspaceState(persistence, new TestRepoFingerprintService());
            using var selection = new RepositorySelectionService(workspaceState, persistence);
            await selection.SelectPathAsync(repositoryPath);
            await WaitForDetectionAsync(selection);

            Assert.True(selection.IsImportEnabled);

            var activity = new ActivityViewModel(workspaceState);
            using var mainViewModel = new MainWindowViewModel(workspaceState, selection, activity, persistence);
            var importCompleted = false;
            mainViewModel.RepositoryImported += () => importCompleted = true;

            var imported = await mainViewModel.CompleteSelectedRepositoryImportAsync();
            var workspace = await persistence.LoadAsync();

            Assert.True(imported);
            Assert.True(importCompleted);
            Assert.NotNull(workspace);
            Assert.Equal(repositoryPath, workspace!.ImportedRepositories[0].RepositoryPath);
            Assert.NotNull(workspace.ActiveProject);
            Assert.Equal(workspace.ActiveProjectId, workspace.ActiveProject!.Id);
            Assert.Equal(workspace.ActiveProjectId, workspace.ImportedRepositories[0].ProjectId);
            Assert.Equal(Path.GetFileName(repositoryPath), workspace.ActiveProject.State.Identity!.Name);
            Assert.Equal(repositoryPath, workspace.ActiveProject.RepositoryPaths[0]);
            Assert.NotNull(workspace.LastBuiltUtc);
            Assert.NotNull(workspace.RepositoryFingerprint);
            Assert.True(mainViewModel.HasActiveProject);
        }
        finally
        {
            Directory.Delete(repositoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Reopening_The_Same_Folder_Resumes_The_Existing_EngineOs_Project()
    {
        var repositoryPath = CreateSupportedRepository();
        try
        {
            var persistence = new InMemoryWorkspacePersistence();
            var workspaceState = new WorkspaceState(persistence, new TestRepoFingerprintService());
            using var selection = new RepositorySelectionService(workspaceState, persistence);

            await selection.SelectPathAsync(repositoryPath);
            await WaitForDetectionAsync(selection);
            Assert.True(await selection.ImportAsync());

            var firstProject = workspaceState.ActiveWorkspace!.ActiveProject!;
            var firstProjectId = firstProject.Id;
            firstProject.State.Identity!.Description = "Accumulated engineering understanding";
            Assert.True(workspaceState.Save());

            await selection.SelectPathAsync(repositoryPath + Path.DirectorySeparatorChar);
            await WaitForDetectionAsync(selection);
            Assert.True(await selection.ImportAsync());

            var resumedWorkspace = workspaceState.ActiveWorkspace!;
            Assert.Single(resumedWorkspace.Projects);
            Assert.Equal(firstProjectId, resumedWorkspace.ActiveProjectId);
            Assert.Equal(firstProjectId, resumedWorkspace.ImportedRepositories[0].ProjectId);
            Assert.Equal("Accumulated engineering understanding", resumedWorkspace.ActiveProject!.State.Identity!.Description);

            var persisted = await persistence.LoadAsync();
            Assert.NotNull(persisted);
            Assert.Single(persisted!.Projects);
            Assert.Equal(firstProjectId, persisted.ActiveProjectId);
            Assert.Equal(firstProjectId, persisted.ImportedRepositories[0].ProjectId);
        }
        finally
        {
            Directory.Delete(repositoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task Close_Project_Persists_Empty_State_And_Returns_To_Launcher_State()
    {
        var persistence = new InMemoryWorkspacePersistence();
        var workspaceState = new WorkspaceState(persistence, new TestRepoFingerprintService());
        var workspace = CreateActiveWorkspace("ExistingProject", "C:\\projects\\ExistingProject");
        workspaceState.ReplaceWorkspace(workspace);
        await persistence.SaveAsync(workspace);

        using var selection = new RepositorySelectionService(workspaceState, persistence);
        using var mainViewModel = new MainWindowViewModel(
            workspaceState,
            selection,
            new ActivityViewModel(workspaceState),
            persistence);

        Assert.True(mainViewModel.HasActiveProject);
        Assert.True(mainViewModel.CloseProject());

        var persisted = await persistence.LoadAsync();
        Assert.NotNull(persisted);
        Assert.Null(persisted!.ActiveProject);
        Assert.False(mainViewModel.HasActiveProject);
        Assert.False(workspaceState.ActiveWorkspace!.ActiveProject is not null);
    }

    [Fact]
    public async Task Switching_Project_Uses_The_Existing_Import_Boundary()
    {
        var firstPath = CreateSupportedRepository();
        var secondPath = CreateSupportedRepository();
        try
        {
            var persistence = new InMemoryWorkspacePersistence();
            var workspaceState = new WorkspaceState(persistence, new TestRepoFingerprintService());
            using var selection = new RepositorySelectionService(workspaceState, persistence);

            await selection.SelectPathAsync(firstPath);
            await WaitForDetectionAsync(selection);
            Assert.True(await selection.ImportAsync());
            var firstProjectId = workspaceState.ActiveWorkspace!.ActiveProjectId;

            await selection.SelectPathAsync(secondPath);
            await WaitForDetectionAsync(selection);
            Assert.True(await selection.ImportAsync());

            Assert.NotEqual(firstProjectId, workspaceState.ActiveWorkspace!.ActiveProjectId);
            Assert.Equal(secondPath, workspaceState.ActiveWorkspace.RepositoryPath);
            Assert.Equal(secondPath, workspaceState.ActiveWorkspace.ActiveProject!.RepositoryPaths[0]);
        }
        finally
        {
            Directory.Delete(firstPath, recursive: true);
            Directory.Delete(secondPath, recursive: true);
        }
    }

    [Fact]
    public async Task Failed_Project_Switch_Preserves_The_Current_Active_Project()
    {
        var repositoryPath = CreateSupportedRepository();
        try
        {
            var persistence = new FailingWorkspacePersistence();
            var workspaceState = new WorkspaceState(persistence, new TestRepoFingerprintService());
            var existing = CreateActiveWorkspace("ExistingProject", "C:\\projects\\ExistingProject");
            workspaceState.ReplaceWorkspace(existing);
            await persistence.SaveAsync(existing);
            var existingProjectId = existing.ActiveProjectId;
            persistence.FailSaves = true;

            using var selection = new RepositorySelectionService(workspaceState, persistence);
            await selection.SelectPathAsync(repositoryPath);

            var imported = await selection.ImportAsync();

            Assert.False(imported);
            Assert.Equal(existingProjectId, workspaceState.ActiveWorkspace!.ActiveProjectId);
            Assert.Equal("ExistingProject", workspaceState.ActiveWorkspace.ActiveProject!.State.Identity!.Name);
            Assert.Equal("C:\\projects\\ExistingProject", workspaceState.ActiveWorkspace.RepositoryPath);
            Assert.Equal(existingProjectId, (await persistence.LoadAsync())!.ActiveProjectId);
        }
        finally
        {
            Directory.Delete(repositoryPath, recursive: true);
        }
    }

    private static Workspace CreateActiveWorkspace(string name, string repositoryPath)
    {
        var workspace = new Workspace { RepositoryPath = repositoryPath };
        var project = new Project();
        project.State.Identity = new ProjectIdentity { Name = name };
        project.RepositoryPaths.Add(repositoryPath);
        workspace.Projects.Add(project);
        workspace.ActiveProjectId = project.Id;
        workspace.ProjectState = project.State;
        return workspace;
    }

    private static string ReadEngineeringWorkspaceXaml() =>
        ReadWpfFile(Path.Combine("Views", "EngineeringWorkspace.xaml"));

    private static string ReadWpfFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "EngineeringDiscovery.Wpf", relativePath);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate root WPF file '{relativePath}'.");
    }

    private static async Task WaitForDetectionAsync(RepositorySelectionService selection)
    {
        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (selection.IsDetecting && DateTime.UtcNow < timeout)
        {
            await Task.Delay(25);
        }

        Assert.False(selection.IsDetecting);
        Assert.True(selection.IsImportEnabled);
    }

    private static string CreateSupportedRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), "EngineeringDiscovery-WpfTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(path, "Program.cs"), "public static class Program { public static void Main() { } }");
        return path;
    }

    private sealed class FailingWorkspacePersistence : IWorkspacePersistence
    {
        private Workspace? _workspace;

        public bool FailSaves { get; set; }

        public Task<Workspace?> LoadAsync() => Task.FromResult(_workspace);

        public Task SaveAsync(Workspace? workspace)
        {
            if (FailSaves) throw new IOException("Simulated persistence failure.");
            _workspace = workspace;
            return Task.CompletedTask;
        }
    }
}
