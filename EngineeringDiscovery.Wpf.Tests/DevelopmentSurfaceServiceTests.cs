using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EngineeringDiscovery.Wpf.Models;
using EngineeringDiscovery.Wpf.Services;
using EngineeringDiscovery.Wpf.ViewModels;
using Xunit;

namespace EngineeringDiscovery.Wpf.Tests;

public sealed class DevelopmentSurfaceServiceTests
{
    [Fact]
    public async Task RepositoryFileService_EnumeratesAndSearchesRepositoryContent()
    {
        var root = CreateTempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            await File.WriteAllTextAsync(Path.Combine(root, "src", "Feature.cs"), "public class Feature { }\n");
            await File.WriteAllTextAsync(Path.Combine(root, "bin", "ignored.dll"), "ignored");

            var service = new RepositoryFileService();
            var tree = await service.GetTreeAsync(root);
            var fileNames = tree.Children.SelectMany(child => child.Children).Select(child => child.Name).ToArray();
            var matches = await service.SearchTextAsync(root, "Feature");
            var fileMatches = await service.SearchFileNamesAsync(root, "Feature");

            Assert.Contains(tree.Children, node => node.Name == "src");
            Assert.DoesNotContain(tree.Children, node => node.Name == "bin");
            Assert.DoesNotContain(tree.Children, node => node.Name == ".git");
            Assert.Contains("Feature.cs", fileNames);
            Assert.Single(matches);
            Assert.Equal(1, matches[0].LineNumber);
            Assert.Single(fileMatches);
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public async Task DocumentViewModel_TracksDirtyStateAndSavesChanges()
    {
        var root = CreateTempDirectory();
        try
        {
            var path = Path.Combine(root, "Feature.cs");
            await File.WriteAllTextAsync(path, "old");
            var document = new DocumentViewModel(path, root, new RepositoryFileService());

            await document.LoadAsync();
            document.UpdateFromEditor("new");
            Assert.True(document.IsDirty);

            await document.SaveAsync();

            Assert.False(document.IsDirty);
            Assert.Equal("new", await File.ReadAllTextAsync(path));
        }
        finally
        {
            DeleteTempDirectory(root);
        }
    }

    [Fact]
    public void GitChangesParser_MapsTrackedAndUntrackedStatuses()
    {
        var changes = GitChangesParser.Parse(
            "C:\\repo",
            "## main...origin/main\n M src\\Changed.cs\nA  Added.cs\n?? notes.txt\n D Removed.cs\n");

        Assert.Equal(4, changes.Count);
        Assert.Equal(GitChangeKind.Modified, changes[0].Kind);
        Assert.False(changes[0].IsStaged);
        Assert.Equal(GitChangeKind.Added, changes[1].Kind);
        Assert.True(changes[1].IsStaged);
        Assert.True(changes[2].IsUntracked);
        Assert.True(changes[3].IsDeleted);
    }

    [Fact]
    public void DevelopmentCommandService_ParsesCompilerDiagnostic()
    {
        var problem = DevelopmentCommandService.ParseProblem(
            "src\\Feature.cs(12,8): error CS1002: ; expected",
            "C:\\repo",
            isError: true,
            DevelopmentCommandKind.Build);

        Assert.NotNull(problem);
        Assert.Equal(ProblemSeverity.Error, problem!.Severity);
        Assert.Equal("CS1002", problem.Code);
        Assert.Equal(12, problem.Line);
        Assert.Equal(8, problem.Column);
        Assert.EndsWith(Path.Combine("src", "Feature.cs"), problem.FilePath, StringComparison.OrdinalIgnoreCase);
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
