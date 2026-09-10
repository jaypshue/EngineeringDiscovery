using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Wpf.Models;

namespace EngineeringDiscovery.Wpf.Services;

public interface IDevelopmentCommandService
{
    Task<DevelopmentCommandResult> RunAsync(
        DevelopmentCommandKind kind,
        string repositoryPath,
        Action<DevelopmentOutputEntry>? outputReceived = null,
        CancellationToken cancellationToken = default);
}

public sealed class DevelopmentCommandService : IDevelopmentCommandService
{
    private static readonly Regex DiagnosticPattern = new(
        @"^(?<file>.*?)(?:\((?<line>\d+)(?:,(?<column>\d+))?\))?:\s*(?<severity>error|warning)\s*(?<code>[A-Za-z]+\d+)?\s*:?[ ]*(?<message>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<DevelopmentCommandResult> RunAsync(
        DevelopmentCommandKind kind,
        string repositoryPath,
        Action<DevelopmentOutputEntry>? outputReceived = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            throw new DirectoryNotFoundException($"Repository folder was not found: {repositoryPath}");

        var target = FindTarget(repositoryPath);
        var verb = kind == DevelopmentCommandKind.Build ? "build" : "test";
        var arguments = new List<string> { verb };
        if (target is not null) arguments.Add(target);
        var command = $"dotnet {verb}{(target is null ? string.Empty : $" {target}")}";
        var problems = new List<ProblemItem>();
        var outputLines = new List<string>();
        var stopwatch = Stopwatch.StartNew();

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
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new InvalidOperationException("The .NET command could not be started.");

        async Task ReadOutputAsync(StreamReader reader, bool isError)
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                lock (outputLines) outputLines.Add(line);
                var problem = ParseProblem(line, repositoryPath, isError, kind);
                if (problem is not null)
                {
                    lock (problems) problems.Add(problem);
                }
                outputReceived?.Invoke(new DevelopmentOutputEntry(DateTimeOffset.Now, kind == DevelopmentCommandKind.Build ? DevelopmentOutputChannel.Build : DevelopmentOutputChannel.Test, line, isError));
            }
        }

        try
        {
            await Task.WhenAll(ReadOutputAsync(process.StandardOutput, false), ReadOutputAsync(process.StandardError, true), process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
        }
        catch
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        stopwatch.Stop();
        return new DevelopmentCommandResult(kind, command, process.ExitCode, stopwatch.Elapsed, problems, string.Join(Environment.NewLine, outputLines));
    }

    public static ProblemItem? ParseProblem(string line, string repositoryPath, bool isError, DevelopmentCommandKind kind)
    {
        var match = DiagnosticPattern.Match(line.Trim());
        if (!match.Success)
        {
            if (kind == DevelopmentCommandKind.Test && line.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                return new ProblemItem(ProblemSeverity.Error, line.Trim(), Source: "dotnet test");
            return isError && !string.IsNullOrWhiteSpace(line)
                ? new ProblemItem(ProblemSeverity.Error, line.Trim(), Source: kind == DevelopmentCommandKind.Build ? "dotnet build" : "dotnet test")
                : null;
        }

        var file = match.Groups["file"].Value.Trim();
        var fullPath = string.IsNullOrWhiteSpace(file) ? null : Path.GetFullPath(Path.Combine(repositoryPath, file));
        return new ProblemItem(
            string.Equals(match.Groups["severity"].Value, "warning", StringComparison.OrdinalIgnoreCase) ? ProblemSeverity.Warning : ProblemSeverity.Error,
            match.Groups["message"].Value.Trim(),
            fullPath,
            ParseInt(match.Groups["line"].Value),
            ParseInt(match.Groups["column"].Value),
            string.IsNullOrWhiteSpace(match.Groups["code"].Value) ? null : match.Groups["code"].Value,
            kind == DevelopmentCommandKind.Build ? "dotnet build" : "dotnet test");
    }

    private static string? FindTarget(string repositoryPath)
    {
        foreach (var pattern in new[] { "*.slnx", "*.sln", "*.csproj" })
        {
            var target = Directory.EnumerateFiles(repositoryPath, pattern, SearchOption.TopDirectoryOnly);
            using var enumerator = target.GetEnumerator();
            if (enumerator.MoveNext()) return Path.GetFileName(enumerator.Current);
        }
        return null;
    }

    private static int? ParseInt(string value) => int.TryParse(value, out var result) ? result : null;
}
