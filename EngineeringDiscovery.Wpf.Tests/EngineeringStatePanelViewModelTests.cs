using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.ProjectState;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Services;
using EngineeringDiscovery.Wpf.ViewModels;
using Moq;
using Xunit;

namespace EngineeringDiscovery.Wpf.Tests;

public sealed class EngineeringStatePanelViewModelTests
{
    [Fact]
    public async Task Projection_Uses_Authoritative_Recent_Work_And_Git_Status()
    {
        var engagement = new WorkerEngagement
        {
            WorkerName = "Developer",
            TaskDescription = "Implement repository import",
            Summary = "Imported the active repository and persisted its investigation.",
            Outcome = EngagementOutcome.Completed,
            Acceptance = AcceptanceStatus.Accepted,
            StartedUtc = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc),
            FilesChanged = new List<string> { "README.md", "src/App.cs" }
        };
        var query = new Mock<IEngineeringStateQuery>();
        query.Setup(q => q.GetProjectIdentity()).Returns(new ProjectIdentity { Name = "EngineeringDiscovery" });
        query.Setup(q => q.GetLifecycle()).Returns(new ProjectLifecycle
        {
            Phase = LifecyclePhase.ActiveDevelopment,
            CurrentFocus = "Desktop state surface"
        });
        query.Setup(q => q.GetRecentEngagements(5)).Returns(new List<WorkerEngagement> { engagement });
        query.Setup(q => q.GetOpenIssues()).Returns(new List<KnownIssue>
        {
            new() { Title = "Follow-up", Status = IssueStatus.Open }
        });
        query.Setup(q => q.GetResumePoint()).Returns(new ResumePoint
        {
            Summary = "Continue the desktop vertical slice.",
            NextRecommendedAction = "Run focused WPF tests."
        });
        query.Setup(q => q.GetWorkspaceContext()).Returns(new WorkspaceContext
        {
            HasRepository = true,
            RepositoryName = "EngineeringDiscovery",
            RepositoryPath = "C:\\projects\\EngineeringDiscovery"
        });

        var git = new StubGitStatusService(new GitStatusSnapshot(
            "C:\\projects\\EngineeringDiscovery",
            true,
            "main",
            false,
            2,
            1,
            3,
            1,
            "abc1234 Add desktop state surface",
            null));
        var workspaceState = CreateWorkspaceState();
        var vm = new EngineeringStatePanelViewModel(query.Object, git, workspaceState);

        await vm.RefreshAsync();

        Assert.Equal("EngineeringDiscovery", vm.ProjectName);
        Assert.Equal("ENGINEERINGDISCOVERY", vm.ProjectNameDisplay);
        Assert.Equal("ActiveDevelopment · Desktop state surface", vm.Lifecycle);
        Assert.Equal(1, vm.OpenIssueCount);
        var recent = Assert.Single(vm.RecentWork);
        Assert.Equal("Implement repository import", recent.Title);
        Assert.Equal("Accepted", recent.Status);
        Assert.Equal("2 files", recent.FilesChangedText);
        Assert.Equal("main", vm.GitStatus.Branch);
        Assert.Equal("Changes present", vm.GitStatus.WorkingTreeState);
        Assert.Equal(2, vm.GitStatus.StagedChanges);
        Assert.Equal(1, vm.GitStatus.ModifiedFiles);
        Assert.Equal(3, vm.GitStatus.UntrackedFiles);
        Assert.Equal(1, vm.GitStatus.DeletedFiles);
        Assert.Equal("C:\\projects\\EngineeringDiscovery", git.LastRequestedPath);
    }

    [Fact]
    public async Task Projection_Shows_Resume_Point_When_No_Engagements_Exist()
    {
        var query = new Mock<IEngineeringStateQuery>();
        query.Setup(q => q.GetRecentEngagements(5)).Returns(Array.Empty<WorkerEngagement>());
        query.Setup(q => q.GetResumePoint()).Returns(new ResumePoint
        {
            Summary = "Repository import is ready to verify.",
            NextRecommendedAction = "Inspect Git status."
        });
        query.Setup(q => q.GetOpenIssues()).Returns(Array.Empty<KnownIssue>());
        query.Setup(q => q.GetWorkspaceContext()).Returns(new WorkspaceContext());

        var vm = new EngineeringStatePanelViewModel(
            query.Object,
            new StubGitStatusService(GitStatusSnapshot.Empty("No repository selected.")),
            CreateWorkspaceState());

        await vm.RefreshAsync();

        var recent = Assert.Single(vm.RecentWork);
        Assert.Equal("Resume point", recent.Title);
        Assert.Equal("Ready to resume", recent.Status);
        Assert.Equal("Repository import is ready to verify.", recent.Summary);
    }

    [Fact]
    public async Task GitStatusService_Returns_Visible_Error_For_Missing_Repository()
    {
        var service = new GitStatusService(TimeSpan.FromMilliseconds(250));

        var result = await service.GetStatusAsync("C:\\path\\that\\does\\not\\exist");

        Assert.False(result.IsRepository);
        Assert.Equal("Unavailable", result.WorkingTreeState);
        Assert.Contains("not available", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkspaceState CreateWorkspaceState() =>
        new(new InMemoryWorkspacePersistence(), new TestRepoFingerprintService());

    private sealed class StubGitStatusService : IGitStatusService
    {
        private readonly GitStatusSnapshot _snapshot;

        public StubGitStatusService(GitStatusSnapshot snapshot) => _snapshot = snapshot;

        public string? LastRequestedPath { get; private set; }

        public Task<GitStatusSnapshot> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default)
        {
            LastRequestedPath = repositoryPath;
            return Task.FromResult(_snapshot);
        }
    }
}
