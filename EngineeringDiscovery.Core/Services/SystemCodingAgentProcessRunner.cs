using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Iteration;

namespace EngineeringDiscovery.Core.Services;

/// <summary>
/// Runs a provider-owned executable with redirected output, a working directory,
/// cancellation, and an absolute timeout. It does not expose arbitrary commands to callers.
/// </summary>
public sealed class SystemCodingAgentProcessRunner : ICodingAgentProcessRunner
{
    private readonly HashSet<string> _allowedProviderIds;
    private readonly Dictionary<string, string> _allowedProviderExecutables;

    public SystemCodingAgentProcessRunner(
        IEnumerable<string>? allowedProviderIds = null,
        IReadOnlyDictionary<string, string>? allowedProviderExecutables = null)
    {
        var providerIds = allowedProviderIds is null
            ? new List<string> { "github-copilot-cli" }
            : new List<string>(allowedProviderIds);
        _allowedProviderIds = new HashSet<string>(providerIds, StringComparer.OrdinalIgnoreCase);
        _allowedProviderExecutables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var providerId in _allowedProviderIds)
        {
            if (allowedProviderExecutables is not null &&
                allowedProviderExecutables.TryGetValue(providerId, out var executable))
            {
                _allowedProviderExecutables[providerId] = executable;
            }
            else if (string.Equals(providerId, "github-copilot-cli", StringComparison.OrdinalIgnoreCase))
            {
                _allowedProviderExecutables[providerId] = "copilot";
            }
        }
    }

    public async Task<CodingAgentProcessResult> RunAsync(
        CodingAgentProcessStartInfo startInfo,
        Action<CodingAgentOutputChunk>? output,
        CancellationToken cancellationToken = default)
    {
        if (startInfo is null) throw new ArgumentNullException(nameof(startInfo));
        if (!_allowedProviderIds.Contains(startInfo.ProviderId) ||
            !_allowedProviderExecutables.TryGetValue(startInfo.ProviderId, out var registeredExecutable) ||
            !string.Equals(startInfo.Executable, registeredExecutable, StringComparison.OrdinalIgnoreCase))
        {
            return new CodingAgentProcessResult(
                CodingAgentExecutionStatus.Failed,
                string.Empty,
                string.Empty,
                null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                $"Coding-agent provider '{startInfo.ProviderId}' is not registered for executable '{startInfo.Executable}'.");
        }

        var startedUtc = DateTimeOffset.UtcNow;
        if (cancellationToken.IsCancellationRequested)
        {
            return new CodingAgentProcessResult(
                CodingAgentExecutionStatus.Cancelled,
                string.Empty,
                string.Empty,
                null,
                startedUtc,
                DateTimeOffset.UtcNow,
                "Coding-agent handoff was cancelled before the process started.");
        }

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = startInfo.Executable,
                WorkingDirectory = startInfo.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        foreach (var argument in startInfo.Arguments)
            process.StartInfo.ArgumentList.Add(argument);

        var standardOutputTask = Task.CompletedTask;
        var standardErrorTask = Task.CompletedTask;

        try
        {
            if (!process.Start())
            {
                return Failed(startedUtc, "The coding-agent process did not start.");
            }

            standardOutputTask = ReadStreamAsync(
                process.StandardOutput,
                standardOutput,
                isError: false,
                output);
            standardErrorTask = ReadStreamAsync(
                process.StandardError,
                standardError,
                isError: true,
                output);

            var waitTask = process.WaitForExitAsync();
            var timeoutTask = Task.Delay(startInfo.Timeout);
            var cancellationTask = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var completedTask = await Task.WhenAny(waitTask, timeoutTask, cancellationTask).ConfigureAwait(false);

            if (completedTask == cancellationTask)
            {
                Kill(process);
                await WaitForExitAfterKillAsync(process).ConfigureAwait(false);
                await DrainOutputAsync(standardOutputTask, standardErrorTask).ConfigureAwait(false);
                return new CodingAgentProcessResult(
                    CodingAgentExecutionStatus.Cancelled,
                    standardOutput.ToString(),
                    standardError.ToString(),
                    TryGetExitCode(process),
                    startedUtc,
                    DateTimeOffset.UtcNow,
                    "Coding-agent handoff was cancelled by the user.");
            }

            if (completedTask == timeoutTask)
            {
                Kill(process);
                await WaitForExitAfterKillAsync(process).ConfigureAwait(false);
                await DrainOutputAsync(standardOutputTask, standardErrorTask).ConfigureAwait(false);
                return new CodingAgentProcessResult(
                    CodingAgentExecutionStatus.TimedOut,
                    standardOutput.ToString(),
                    standardError.ToString(),
                    TryGetExitCode(process),
                    startedUtc,
                    DateTimeOffset.UtcNow,
                    $"Coding-agent handoff exceeded the {startInfo.Timeout.TotalMinutes:0.#}-minute timeout.");
            }

            await waitTask.ConfigureAwait(false);
            process.WaitForExit();
            await DrainOutputAsync(standardOutputTask, standardErrorTask).ConfigureAwait(false);
            var exitCode = TryGetExitCode(process);
            var status = exitCode == 0
                ? CodingAgentExecutionStatus.Succeeded
                : CodingAgentExecutionStatus.Failed;
            var failureReason = status == CodingAgentExecutionStatus.Failed
                ? string.IsNullOrWhiteSpace(standardError.ToString())
                    ? $"Coding-agent process exited with code {exitCode}."
                    : standardError.ToString().Trim()
                : null;

            return new CodingAgentProcessResult(
                status,
                standardOutput.ToString(),
                standardError.ToString(),
                exitCode,
                startedUtc,
                DateTimeOffset.UtcNow,
                failureReason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            await WaitForExitAfterKillAsync(process).ConfigureAwait(false);
                await DrainOutputAsync(standardOutputTask, standardErrorTask).ConfigureAwait(false);
            return new CodingAgentProcessResult(
                CodingAgentExecutionStatus.Cancelled,
                standardOutput.ToString(),
                standardError.ToString(),
                TryGetExitCode(process),
                startedUtc,
                DateTimeOffset.UtcNow,
                "Coding-agent handoff was cancelled by the user.");
        }
        catch (Exception ex)
        {
            Kill(process);
            await WaitForExitAfterKillAsync(process).ConfigureAwait(false);
                await DrainOutputAsync(standardOutputTask, standardErrorTask).ConfigureAwait(false);
            return Failed(startedUtc, ex.Message, standardOutput.ToString(), standardError.ToString());
        }
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        StringBuilder buffer,
        bool isError,
        Action<CodingAgentOutputChunk>? output)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            buffer.AppendLine(line);
            output?.Invoke(new CodingAgentOutputChunk(line, isError, DateTimeOffset.UtcNow));
        }
    }

    private static async Task DrainOutputAsync(Task standardOutputTask, Task standardErrorTask)
    {
        try
        {
            await Task.WhenAll(standardOutputTask, standardErrorTask)
                .WaitAsync(TimeSpan.FromSeconds(2))
                .ConfigureAwait(false);
        }
        catch { }
    }

    private static CodingAgentProcessResult Failed(
        DateTimeOffset startedUtc,
        string reason,
        string standardOutput = "",
        string standardError = "") =>
        new(
            CodingAgentExecutionStatus.Failed,
            standardOutput,
            standardError,
            null,
            startedUtc,
            DateTimeOffset.UtcNow,
            reason);

    private static int? TryGetExitCode(Process process)
    {
        try { return process.HasExited ? process.ExitCode : null; }
        catch { return null; }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch { }
    }

    private static async Task WaitForExitAfterKillAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            process.WaitForExit();
        }
        catch { }
    }
}
