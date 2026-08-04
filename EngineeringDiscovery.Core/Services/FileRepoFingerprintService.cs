using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Production repository fingerprint implementation.
    ///
    /// Initial implementation produces a lightweight fingerprint based on the latest
    /// file write timestamp under the repository root and a directory hash of file paths
    /// to detect structural changes. This is intentionally simple and fast; a git-aware
    /// implementation may replace it later without changing WorkspaceState.
    /// </summary>
    public sealed class FileRepoFingerprintService : IRepoFingerprintService
    {
        public Task<string?> ComputeFingerprintAsync(string repositoryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repositoryPath)) return Task.FromResult<string?>(null);
                var dir = new DirectoryInfo(repositoryPath);
                if (!dir.Exists) return Task.FromResult<string?>(null);

                // Compute a simple directory hash: combine file paths and last write times
                // into a stable string and hash it with SHA256 for compactness.
                var files = dir.EnumerateFiles("*", SearchOption.AllDirectories)
                    .OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase);

                var sb = new StringBuilder();
                DateTime? latest = null;
                foreach (var f in files)
                {
                    try
                    {
                        sb.Append(f.FullName.Replace(repositoryPath, string.Empty));
                        sb.Append('|');
                        sb.Append(f.LastWriteTimeUtc.ToString("o"));
                        sb.Append('\n');
                        if (latest is null || f.LastWriteTimeUtc > latest) latest = f.LastWriteTimeUtc;
                    }
                    catch { }
                }

                var hash = ComputeSha256Hash(sb.ToString());
                // Combine latest timestamp and hash so simple timestamp-based heuristics can still be used
                var fingerprint = (latest?.ToString("o") ?? string.Empty) + ":" + hash;
                return Task.FromResult<string?>(fingerprint);
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
        }

        public Task<ModelFreshness> EvaluateFreshnessAsync(string repositoryPath, DateTime? lastBuiltUtc, string? storedFingerprint)
        {
            try
            {
                // If the model was never built, require build
                if (lastBuiltUtc is null) return Task.FromResult(ModelFreshness.RefreshRequired);

                var current = ComputeFingerprintAsync(repositoryPath).GetAwaiter().GetResult();
                if (current is null) return Task.FromResult(ModelFreshness.Unknown);

                if (string.Equals(current, storedFingerprint, StringComparison.Ordinal)) return Task.FromResult(ModelFreshness.Current);

                // If stored fingerprint is missing but model has been built, recommend refresh
                if (string.IsNullOrWhiteSpace(storedFingerprint)) return Task.FromResult(ModelFreshness.RefreshRecommended);

                // Otherwise fingerprint differs -> recommend refresh
                return Task.FromResult(ModelFreshness.RefreshRecommended);
            }
            catch
            {
                return Task.FromResult(ModelFreshness.Unknown);
            }
        }

        private static string ComputeSha256Hash(string raw)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(raw ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
