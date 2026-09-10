using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Web.Services;

/// <summary>
/// Web-host adapter for the small allow-list of local engineering operations.
/// It accepts no command text from conversation: only fixed dotnet build/test and
/// git status invocations are exposed. Coding-agent execution and arbitrary shell
/// access remain unavailable.
/// </summary>
public sealed class WebEngineeringOperationGateway : IEngineeringOperationGateway
{
    private readonly IEngineeringStateQuery _stateQuery;

    public WebEngineeringOperationGateway(IEngineeringStateQuery stateQuery)
    {
        _stateQuery = stateQuery ?? throw new ArgumentNullException(nameof(stateQuery));
    }

    public async Task<EngineeringOperationResult> ExecuteAsync(
        EngineeringOperationKind operation,
        CancellationToken cancellationToken = default)
    {
        var repositoryPath = _stateQuery.GetWorkspaceContext()?.RepositoryPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
        {
            return new EngineeringOperationResult(operation, false, "No loaded repository folder is available, so nothing was executed.");
        }

        return operation switch
        {
            EngineeringOperationKind.Build => await RunDotnetAsync(operation, repositoryPath, "build", cancellationToken).ConfigureAwait(false),
            EngineeringOperationKind.Test => await RunDotnetAsync(operation, repositoryPath, "test", cancellationToken).ConfigureAwait(false),
            EngineeringOperationKind.InspectChanges => await InspectChangesAsync(repositoryPath, cancellationToken).ConfigureAwait(false),
            EngineeringOperationKind.RefreshEvidence => new EngineeringOperationResult(operation, false, "Repository evidence refresh is owned by the repository import workflow on this host. No refresh was executed."),
            _ => new EngineeringOperationResult(operation, false, "That operation is not available through the safe Web conversation boundary.")
        };
    }

    private static async Task<EngineeringOperationResult> RunDotnetAsync(
        EngineeringOperationKind operation,
        string repositoryPath,
        string verb,
        CancellationToken cancellationToken)
    {
        var target = FindTarget(repositoryPath);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add(verb);
        if (target is not null) process.StartInfo.ArgumentList.Add(target);
        if (!process.Start()) throw new InvalidOperationException("The fixed .NET operation could not be started.");

        var output = new List<string>();
        async Task ReadAsync(StreamReader reader)
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                lock (output) output.Add(line);
            }
        }

        try
        {
            await Task.WhenAll(ReadAsync(process.StandardOutput), ReadAsync(process.StandardError), process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var lines = output.Where(line => !string.IsNullOrWhiteSpace(line)).TakeLast(8).ToList();
        var label = operation == EngineeringOperationKind.Build ? "Build" : "Tests";
        return new EngineeringOperationResult(
            operation,
            process.ExitCode == 0,
            $"{label} {(process.ExitCode == 0 ? "passed" : "failed")} · exit code {process.ExitCode}.",
            lines);
    }

    private static async Task<EngineeringOperationResult> InspectChangesAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
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
        process.StartInfo.ArgumentList.Add("status");
        process.StartInfo.ArgumentList.Add("--porcelain=v1");
        process.StartInfo.ArgumentList.Add("--untracked-files=all");
        if (!process.Start()) throw new InvalidOperationException("The fixed Git inspection could not be started.");

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return new EngineeringOperationResult(EngineeringOperationKind.InspectChanges, false, string.IsNullOrWhiteSpace(error) ? "Git inspection failed." : error.Trim());
        }

        var changes = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return new EngineeringOperationResult(
            EngineeringOperationKind.InspectChanges,
            true,
            changes.Length == 0 ? "No current Git changes were found." : $"{changes.Length} current Git change(s) found.",
            changes.Take(30).ToList());
    }

    private static string? FindTarget(string repositoryPath)
    {
        foreach (var pattern in new[] { "*.slnx", "*.sln", "*.csproj" })
        {
            var target = Directory.EnumerateFiles(repositoryPath, pattern, SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (target is not null) return Path.GetFileName(target);
        }

        return null;
    }
}
