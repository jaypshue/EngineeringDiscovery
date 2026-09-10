using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Wpf.Models;

namespace EngineeringDiscovery.Wpf.Services;

public interface IGitChangesService
{
    Task<IReadOnlyList<GitChange>> GetChangesAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<string> GetDiffAsync(string repositoryPath, GitChange change, CancellationToken cancellationToken = default);
}

public sealed class GitChangesService : IGitChangesService
{
    private readonly TimeSpan _timeout;

    public GitChangesService() : this(TimeSpan.FromSeconds(10)) { }

    public GitChangesService(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        _timeout = timeout;
    }

    public async Task<IReadOnlyList<GitChange>> GetChangesAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath)) return Array.Empty<GitChange>();
        var result = await RunGitAsync(repositoryPath, ["status", "--porcelain=v1", "--untracked-files=all"], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0) return Array.Empty<GitChange>();
        return GitChangesParser.Parse(repositoryPath, result.StandardOutput);
    }

    public async Task<string> GetDiffAsync(string repositoryPath, GitChange change, CancellationToken cancellationToken = default)
    {
        if (change.IsUntracked)
        {
            if (!File.Exists(change.FullPath)) return $"Untracked file: {change.RelativePath}";
            var content = await File.ReadAllTextAsync(change.FullPath, cancellationToken).ConfigureAwait(false);
            return CreateUntrackedDiff(change.RelativePath, content);
        }

        var args = change.IsStaged
            ? new[] { "diff", "--cached", "--no-ext-diff", "--no-color", "--", change.RelativePath }
            : new[] { "diff", "--no-ext-diff", "--no-color", "--", change.RelativePath };
        var result = await RunGitAsync(repositoryPath, args, cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StandardOutput)
            ? result.StandardOutput
            : string.IsNullOrWhiteSpace(result.StandardError) ? "No diff available." : result.StandardError.Trim();
    }

    private async Task<GitCommandResult> RunGitAsync(string repositoryPath, string[] arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repositoryPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new InvalidOperationException("Git process could not be started.");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);
        try
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            return new GitCommandResult(process.ExitCode, output.Result, error.Result);
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
    }

    private static string CreateUntrackedDiff(string relativePath, string content)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var body = string.Join(Environment.NewLine, lines.Select(line => $"+{line}"));
        return $"diff --git a/{relativePath} b/{relativePath}{Environment.NewLine}new file mode 100644{Environment.NewLine}--- /dev/null{Environment.NewLine}+++ b/{relativePath}{Environment.NewLine}@@ -0,0 +1,{lines.Length} @@{Environment.NewLine}{body}";
    }

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}

public static class GitChangesParser
{
    public static IReadOnlyList<GitChange> Parse(string repositoryPath, string output)
    {
        var changes = new List<GitChange>();
        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3) continue;
            var indexStatus = line[0];
            var worktreeStatus = line[1];
            if (indexStatus == '#' && worktreeStatus == '#') continue;
            var path = line[3..].Trim();
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (path.Contains(" -> ", StringComparison.Ordinal)) path = path[(path.LastIndexOf(" -> ", StringComparison.Ordinal) + 4)..];

            var isUntracked = indexStatus == '?' && worktreeStatus == '?';
            var isStaged = indexStatus != ' ' && !isUntracked;
            var status = $"{indexStatus}{worktreeStatus}";
            var kind = isUntracked ? GitChangeKind.Untracked : GetKind(indexStatus, worktreeStatus);
            changes.Add(new GitChange(
                path.Replace(Path.DirectorySeparatorChar, '/'),
                Path.Combine(repositoryPath, path),
                kind,
                isStaged,
                isUntracked,
                kind == GitChangeKind.Deleted,
                status));
        }
        return changes;
    }

    private static GitChangeKind GetKind(char indexStatus, char worktreeStatus)
    {
        var status = indexStatus == ' ' ? worktreeStatus : indexStatus;
        return status switch
        {
            'M' => GitChangeKind.Modified,
            'A' => GitChangeKind.Added,
            'D' => GitChangeKind.Deleted,
            'R' => GitChangeKind.Renamed,
            'T' => GitChangeKind.TypeChanged,
            _ => GitChangeKind.Unknown
        };
    }
}
