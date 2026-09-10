using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Wpf.Models;

namespace EngineeringDiscovery.Wpf.Services;

public interface IRepositoryFileService
{
    Task<RepositoryFileNode> GetTreeAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<string> ReadFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task SaveFileAsync(string filePath, string content, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileSearchResult>> SearchFileNamesAsync(string repositoryPath, string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FileSearchResult>> SearchTextAsync(string repositoryPath, string query, CancellationToken cancellationToken = default);
}

public sealed class RepositoryFileService : IRepositoryFileService
{
    private const int MaxSearchResults = 500;
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "node_modules", "packages", "TestResults", "artifacts", ".idea"
    };

    public async Task<RepositoryFileNode> GetTreeAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository folder was not found: {repositoryPath}");

        return await Task.Run(() => BuildTree(repositoryPath, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    public static bool IsProbablyBinaryFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(4096, (int)Math.Max(1, stream.Length))];
            var count = stream.Read(buffer, 0, buffer.Length);
            return buffer.AsSpan(0, count).IndexOf((byte)0) >= 0;
        }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }

    public async Task<string> ReadFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("File was not found.", filePath);
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveFileAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A file path is required.", nameof(filePath));
        await File.WriteAllTextAsync(filePath, content ?? string.Empty, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<FileSearchResult>> SearchFileNamesAsync(string repositoryPath, string query, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<FileSearchResult>>(() =>
        {
            var results = new List<FileSearchResult>();
            if (string.IsNullOrWhiteSpace(query) || !Directory.Exists(repositoryPath)) return results;
            foreach (var file in EnumerateFiles(repositoryPath, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    results.Add(new FileSearchResult(RelativePath(repositoryPath, file.FullName), file.FullName, null, null, null));
                if (results.Count >= MaxSearchResults) break;
            }
            return results;
        }, cancellationToken);

    public Task<IReadOnlyList<FileSearchResult>> SearchTextAsync(string repositoryPath, string query, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<FileSearchResult>>(() =>
        {
            var results = new List<FileSearchResult>();
            if (string.IsNullOrWhiteSpace(query) || !Directory.Exists(repositoryPath)) return results;
            foreach (var file in EnumerateFiles(repositoryPath, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsProbablyBinaryFile(file.FullName)) continue;

                try
                {
                    var lineNumber = 0;
                    foreach (var line in File.ReadLines(file.FullName))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        lineNumber++;
                        var column = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                        if (column < 0) continue;
                        results.Add(new FileSearchResult(
                            RelativePath(repositoryPath, file.FullName),
                            file.FullName,
                            lineNumber,
                            column + 1,
                            line.Trim()));
                        if (results.Count >= MaxSearchResults) return results;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return results;
        }, cancellationToken);

    private static RepositoryFileNode BuildTree(string repositoryPath, CancellationToken cancellationToken)
    {
        var root = new DirectoryInfo(repositoryPath);
        var rootNode = new RepositoryFileNode(root.Name, root.FullName, string.Empty, true);
        AddChildren(rootNode, root, repositoryPath, cancellationToken);
        return rootNode;
    }

    private static void AddChildren(RepositoryFileNode parent, DirectoryInfo directory, string rootPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<DirectoryInfo> directories;
        IEnumerable<FileInfo> files;
        try
        {
            directories = directory.EnumerateDirectories()
                .Where(d => !ExcludedDirectoryNames.Contains(d.Name) && !d.Attributes.HasFlag(FileAttributes.ReparsePoint))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            files = directory.EnumerateFiles()
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        foreach (var childDirectory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var child = new RepositoryFileNode(childDirectory.Name, childDirectory.FullName, RelativePath(rootPath, childDirectory.FullName), true);
            parent.Children.Add(child);
            AddChildren(child, childDirectory, rootPath, cancellationToken);
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            parent.Children.Add(new RepositoryFileNode(file.Name, file.FullName, RelativePath(rootPath, file.FullName), false));
        }
    }

    private static IEnumerable<FileInfo> EnumerateFiles(string rootPath, CancellationToken cancellationToken)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(rootPath));
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IEnumerable<DirectoryInfo> directories;
            IEnumerable<FileInfo> files;
            try
            {
                directories = directory.EnumerateDirectories()
                    .Where(d => !ExcludedDirectoryNames.Contains(d.Name) && !d.Attributes.HasFlag(FileAttributes.ReparsePoint));
                files = directory.EnumerateFiles();
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var child in directories) pending.Push(child);
            foreach (var file in files) yield return file;
        }
    }

    private static string RelativePath(string rootPath, string path) =>
        Path.GetRelativePath(rootPath, path).Replace(Path.DirectorySeparatorChar, '/');
}
