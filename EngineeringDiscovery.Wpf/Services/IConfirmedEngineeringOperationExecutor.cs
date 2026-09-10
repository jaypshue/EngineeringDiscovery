using System.Threading;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Wpf.Models;

namespace EngineeringDiscovery.Wpf.Services;

/// <summary>
/// The narrow WPF execution seam used after Conversation confirmation.
/// Implementations expose only the existing Build/Test capability and return
/// the same command result that the Development Surface records.
/// </summary>
public interface IConfirmedEngineeringOperationExecutor
{
    Task<DevelopmentCommandResult?> ExecuteAsync(
        EngineeringOperationKind operation,
        CancellationToken cancellationToken = default);
}
