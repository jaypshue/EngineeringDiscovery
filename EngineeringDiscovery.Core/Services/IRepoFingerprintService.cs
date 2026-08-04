using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Computes repository fingerprints to determine if the persisted engineering model is stale.
    /// Implementations perform filesystem or repository queries and run in infrastructure layer.
    /// </summary>
    public interface IRepoFingerprintService
    {
        /// <summary>
        /// Compute a fingerprint string for the repository at repositoryPath. Returns null if fingerprint cannot be computed.
        /// </summary>
        Task<string?> ComputeFingerprintAsync(string repositoryPath);
    }
}
