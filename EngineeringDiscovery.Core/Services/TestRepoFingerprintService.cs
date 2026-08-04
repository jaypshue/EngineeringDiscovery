using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Workspace;

namespace EngineeringDiscovery.Core.Services
{
    // Simple in-memory test implementation used by unit tests.
    public sealed class TestRepoFingerprintService : IRepoFingerprintService
    {
        public Task<string?> ComputeFingerprintAsync(string repositoryPath)
        {
            // Deterministic synthetic fingerprint for tests based on path
            if (string.IsNullOrWhiteSpace(repositoryPath)) return Task.FromResult<string?>(null);
            return Task.FromResult<string?>("TESTFP:" + repositoryPath.GetHashCode().ToString("X8"));
        }

        public Task<ModelFreshness> EvaluateFreshnessAsync(string repositoryPath, DateTime? lastBuiltUtc, string? storedFingerprint)
        {
            // If last built not present, require build
            if (lastBuiltUtc is null) return Task.FromResult(ModelFreshness.RefreshRequired);
            // Compute a fingerprint and compare
            var fp = ComputeFingerprintAsync(repositoryPath).GetAwaiter().GetResult();
            if (fp is null) return Task.FromResult(ModelFreshness.Unknown);
            if (!string.Equals(fp, storedFingerprint, StringComparison.Ordinal)) return Task.FromResult(ModelFreshness.RefreshRecommended);
            return Task.FromResult(ModelFreshness.Current);
        }
    }
}
