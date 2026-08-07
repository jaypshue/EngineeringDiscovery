using System.Threading.Tasks;

namespace EngineeringDiscovery.Wpf.Services
{
    // Lightweight orchestration entry point for engineering activities.
    // The Engine coordinates services (discovery, etc.) and lets the existing
    // event-driven pipeline update evidence, understanding, and state.
    public class EngineeringEngine
    {
        private readonly RepositoryDiscoveryService _repositoryDiscovery;

        public EngineeringEngine(RepositoryDiscoveryService? repositoryDiscovery = null)
        {
            _repositoryDiscovery = repositoryDiscovery ?? new RepositoryDiscoveryService();
        }

        // Begin repository discovery. This coordinates the discovery service and
        // relies on the existing event bus to propagate observations into evidence
        // and understanding. Keep implementation intentionally lightweight.
        public async Task BeginRepositoryDiscovery(string repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath)) return;

            await _repositoryDiscovery.DiscoverAsync(repositoryPath).ConfigureAwait(false);
        }
    }
}
