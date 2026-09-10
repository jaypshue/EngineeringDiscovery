using System.Threading;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Wpf.Services;

public interface IGitStatusService
{
    Task<GitStatusSnapshot> GetStatusAsync(string repositoryPath, CancellationToken cancellationToken = default);
}
