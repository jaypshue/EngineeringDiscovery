using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Wpf.Services;

/// <summary>
/// Reads only the bounded Git metadata needed by the desktop engineering-state surface.
/// Paths are passed through ProcessStartInfo.ArgumentList; no shell is involved.
/// </summary>
public sealed class GitStatusService : IGitStatusService
{
    private readonly TimeSpan _timeout;

    public GitStatusService()
        : this(TimeSpan.FromSeconds(5))
    {
    }

    public GitStatusService(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        _timeout = timeout;
    }

    public async Task<GitStatusSnapshot> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return GitStatusSnapshot.Empty("No repository selected.");
        }

        if (!Directory.Exists(repositoryPath))
        {
            return GitStatusSnapshot.Empty("Repository path is not available.") with { RepositoryPath = repositoryPath };
        }

        try
        {
            var statusResult = await RunGitAsync(repositoryPath, ["status", "--porcelain=v1", "--branch"], cancellationToken);
            if (statusResult.ExitCode != 0)
            {
                return GitStatusSnapshot.Empty(GetError(statusResult, "Git status could not be read.")) with { RepositoryPath = repositoryPath };
            }

            var lines = statusResult.StandardOutput
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var branch = ParseBranch(lines);
            var staged = 0;
            var modified = 0;
            var untracked = 0;
            var deleted = 0;
            var fileStatusLines = lines.Where(line => !line.StartsWith("##", StringComparison.Ordinal));

            foreach (var line in fileStatusLines)
            {
                if (line.Length < 2) continue;
                var indexStatus = line[0];
                var worktreeStatus = line[1];

                if (indexStatus == '?' && worktreeStatus == '?')
                {
                    untracked++;
                    continue;
                }

                if (indexStatus != ' ') staged++;
                if (worktreeStatus == 'M') modified++;
                if (indexStatus == 'D' || worktreeStatus == 'D') deleted++;
            }

            var logResult = await RunGitAsync(repositoryPath, ["log", "-1", "--pretty=format:%h %s"], cancellationToken);
            var mostRecentCommit = logResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(logResult.StandardOutput)
                ? logResult.StandardOutput.Trim()
                : "—";

            return new GitStatusSnapshot(
                repositoryPath,
                true,
                branch,
                staged == 0 && modified == 0 && untracked == 0 && deleted == 0,
                staged,
                modified,
                untracked,
                deleted,
                mostRecentCommit,
                null);
        }
        catch (OperationCanceledException)
        {
            return GitStatusSnapshot.Empty("Git status timed out.") with { RepositoryPath = repositoryPath };
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return GitStatusSnapshot.Empty("Git status is unavailable.") with { RepositoryPath = repositoryPath };
        }
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

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (!process.Start())
        {
            throw new InvalidOperationException("Git process could not be started.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(timeoutCts.Token);
            await Task.WhenAll(standardOutputTask, standardErrorTask);
            return new GitCommandResult(process.ExitCode, standardOutputTask.Result, standardErrorTask.Result);
        }
        catch
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cleanup after a timeout or cancellation.
            }

            throw;
        }
    }

    private static string ParseBranch(string[] lines)
    {
        var branchLine = lines.FirstOrDefault(line => line.StartsWith("## ", StringComparison.Ordinal));
        if (branchLine is null) return "—";

        var branch = branchLine[3..];
        var trackingSeparator = branch.IndexOf("...", StringComparison.Ordinal);
        if (trackingSeparator >= 0) branch = branch[..trackingSeparator];
        return string.IsNullOrWhiteSpace(branch) ? "—" : branch;
    }

    private static string GetError(GitCommandResult result, string fallback)
    {
        return string.IsNullOrWhiteSpace(result.StandardError) ? fallback : result.StandardError.Trim();
    }

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
