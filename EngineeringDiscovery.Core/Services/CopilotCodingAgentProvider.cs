using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services;

/// <summary>
/// GitHub Copilot CLI adapter. It uses Copilot's supported programmatic -p mode
/// and never accepts an executable or shell command from a user prompt.
/// </summary>
public sealed class CopilotCodingAgentProvider : ICodingAgentProvider
{
    private readonly ICodingAgentProcessRunner _processRunner;

    public CopilotCodingAgentProvider(
        ICodingAgentProcessRunner processRunner,
        CodingAgentProviderConfiguration? configuration = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        Configuration = configuration ?? CodingAgentProviderConfiguration.GitHubCopilot();
        if (string.IsNullOrWhiteSpace(Configuration.Executable))
            throw new ArgumentException("A Copilot executable is required.", nameof(configuration));
    }

    public CodingAgentProviderConfiguration Configuration { get; }

    public async Task<CodingAgentProviderAvailability> CheckAvailabilityAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _processRunner.RunAsync(
            new CodingAgentProcessStartInfo(
                Configuration.ProviderId,
                Configuration.Executable,
                new[] { "--version" },
                Environment.CurrentDirectory,
                TimeSpan.FromSeconds(15)),
            output: null,
            cancellationToken).ConfigureAwait(false);

        return result.Status == EngineeringDiscovery.Core.Domain.Iteration.CodingAgentExecutionStatus.Succeeded
            ? new CodingAgentProviderAvailability(true, null)
            : new CodingAgentProviderAvailability(
                false,
                string.IsNullOrWhiteSpace(result.FailureReason)
                    ? "GitHub Copilot CLI is not available."
                    : result.FailureReason,
                result.Status,
                result.StandardError);
    }

    public Task<CodingAgentProcessResult> ExecuteAsync(
        CodingAgentRequest request,
        Action<CodingAgentOutputChunk>? output,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Prompt)) throw new ArgumentException("A prompt is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.WorkingDirectory)) throw new ArgumentException("A repository working directory is required.", nameof(request));

        var arguments = new List<string>
        {
            "-p",
            request.Prompt,
            "-s",
            "--no-ask-user"
        };
        foreach (var tool in Configuration.AllowedTools.Where(tool => !string.IsNullOrWhiteSpace(tool)))
        {
            arguments.Add("--allow-tool");
            arguments.Add(tool);
        }

        var startInfo = new CodingAgentProcessStartInfo(
            Configuration.ProviderId,
            Configuration.Executable,
            arguments,
            request.WorkingDirectory,
            request.Timeout ?? Configuration.DefaultTimeout);
        return _processRunner.RunAsync(startInfo, output, cancellationToken);
    }
}
