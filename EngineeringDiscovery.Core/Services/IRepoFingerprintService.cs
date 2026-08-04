using System;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Freshness classification returned by fingerprinting evaluation.
    /// </summary>
    public enum ModelFreshness
    {
        Unknown,
        Current,
        RefreshRecommended,
        RefreshRequired
    }


    /// <summary>
    /// Computes repository fingerprints and evaluates repository/model freshness.
    /// Implementations perform filesystem or repository queries and run in infrastructure layer.
    /// </summary>
    public interface IRepoFingerprintService
    {
        /// <summary>
        /// Compute a fingerprint string for the repository at repositoryPath. Returns null if fingerprint cannot be computed.
        /// </summary>
        Task<string?> ComputeFingerprintAsync(string repositoryPath);

        /// <summary>
        /// Evaluate freshness given repository location and existing workspace metadata.
        /// </summary>
        Task<ModelFreshness> EvaluateFreshnessAsync(string repositoryPath, DateTime? lastBuiltUtc, string? storedFingerprint);
    }
}
