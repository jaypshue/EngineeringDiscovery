using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Services;

namespace EngineeringDiscovery.Core.Tests.Tests
{
    public class TestFingerprintService : IRepoFingerprintService
    {
        public Task<string?> ComputeFingerprintAsync(string repositoryPath) => Task.FromResult<string?>("fingerprint");

        public Task<ModelFreshness> EvaluateFreshnessAsync(string repositoryPath, DateTime? lastBuiltUtc, string? storedFingerprint)
        {
            return Task.FromResult(ModelFreshness.Current);
        }
    }
}
