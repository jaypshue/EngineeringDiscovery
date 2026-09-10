using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Models;

namespace EngineeringDiscovery.Wpf.Services;

/// <summary>
/// Adapts the existing WPF development services to the shared conversation
/// capability boundary. Only the fixed build, test, repository-refresh, and
/// git-inspection operations are exposed; no arbitrary process is accepted.
/// </summary>
public sealed class WpfEngineeringOperationGateway : IEngineeringOperationGateway
{
    private readonly IEngineeringStateQuery _stateQuery;
    private readonly IRepositoryFileService _fileService;
    private readonly IGitChangesService _gitChangesService;
    private readonly IDevelopmentCommandService _commandService;

    public WpfEngineeringOperationGateway(
        IEngineeringStateQuery stateQuery,
        IRepositoryFileService fileService,
        IGitChangesService gitChangesService,
        IDevelopmentCommandService commandService)
    {
        _stateQuery = stateQuery ?? throw new ArgumentNullException(nameof(stateQuery));
        _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        _gitChangesService = gitChangesService ?? throw new ArgumentNullException(nameof(gitChangesService));
        _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
    }

    public async Task<EngineeringOperationResult> ExecuteAsync(
        EngineeringOperationKind operation,
        CancellationToken cancellationToken = default)
    {
        var repositoryPath = _stateQuery.GetWorkspaceContext()?.RepositoryPath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return new EngineeringOperationResult(operation, false, "No repository is loaded, so nothing was executed.");
        }

        return operation switch
        {
            EngineeringOperationKind.Build => await RunCommandAsync(DevelopmentCommandKind.Build, repositoryPath, cancellationToken).ConfigureAwait(false),
            EngineeringOperationKind.Test => await RunCommandAsync(DevelopmentCommandKind.Test, repositoryPath, cancellationToken).ConfigureAwait(false),
            EngineeringOperationKind.RefreshEvidence => await RefreshEvidenceAsync(repositoryPath, cancellationToken).ConfigureAwait(false),
            EngineeringOperationKind.InspectChanges => await InspectChangesAsync(repositoryPath, cancellationToken).ConfigureAwait(false),
            _ => new EngineeringOperationResult(operation, false, "That operation is not available through the safe conversation boundary.")
        };
    }

    private async Task<EngineeringOperationResult> RunCommandAsync(
        DevelopmentCommandKind kind,
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var result = await _commandService.RunAsync(kind, repositoryPath, cancellationToken: cancellationToken).ConfigureAwait(false);
        var details = result.Problems
            .Take(8)
            .Select(problem => $"{problem.Severity}: {problem.Message}{(string.IsNullOrWhiteSpace(problem.DisplayLocation) ? string.Empty : $" ({problem.DisplayLocation})")}")
            .ToList();
        return new EngineeringOperationResult(
            kind == DevelopmentCommandKind.Build ? EngineeringOperationKind.Build : EngineeringOperationKind.Test,
            result.Succeeded,
            $"{result.DisplayStatus} · exit code {result.ExitCode} · {result.Problems.Count} reported problem(s) · {result.Duration.TotalSeconds:0.0}s",
            details);
    }

    private async Task<EngineeringOperationResult> RefreshEvidenceAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var root = await _fileService.GetTreeAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        var changes = await _gitChangesService.GetChangesAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        return new EngineeringOperationResult(
            EngineeringOperationKind.RefreshEvidence,
            true,
            $"Repository evidence refreshed for {root.Name}: {root.Children.Count} top-level item(s), {changes.Count} changed file(s).");
    }

    private async Task<EngineeringOperationResult> InspectChangesAsync(
        string repositoryPath,
        CancellationToken cancellationToken)
    {
        var changes = await _gitChangesService.GetChangesAsync(repositoryPath, cancellationToken).ConfigureAwait(false);
        var details = changes
            .Take(30)
            .Select(change => change.DisplayName)
            .ToList();
        return new EngineeringOperationResult(
            EngineeringOperationKind.InspectChanges,
            true,
            changes.Count == 0 ? "No current Git changes were found." : $"{changes.Count} current Git change(s) found.",
            details);
    }
}
