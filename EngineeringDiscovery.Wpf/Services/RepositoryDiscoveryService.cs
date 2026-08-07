using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EngineeringDiscovery.Wpf.Events;
using EngineeringDiscovery.Wpf.Models;

namespace EngineeringDiscovery.Wpf.Services
{
    // Lightweight repository discovery service that scans the repository root for solution and project files
    // and publishes engineering events and evidence. This is intentionally simple and synchronous/fast.
    public class RepositoryDiscoveryService
    {
        public async Task DiscoverAsync(string repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            {
                return;
            }

            // Publish RepositoryDiscovered event
            EngineeringEventBus.Publish(new EngineeringEvent(EngineeringEventType.RepositoryDiscovered, repositoryPath));

            // Find solution files
            var slnFiles = Directory.EnumerateFiles(repositoryPath, "*.sln", SearchOption.TopDirectoryOnly).ToList();
            if (slnFiles.Any())
            {
                // For simplicity, take the first solution found
                EngineeringEventBus.Publish(new EngineeringEvent(EngineeringEventType.SolutionDiscovered, slnFiles.First()));
            }

            // Find project files under repository
            var projFiles = Directory.EnumerateFiles(repositoryPath, "*.csproj", SearchOption.AllDirectories).ToList();
            foreach (var p in projFiles)
            {
                EngineeringEventBus.Publish(new EngineeringEvent(EngineeringEventType.ProjectDiscovered, p));
            }

            // Mark analysis completed
            EngineeringEventBus.Publish(new EngineeringEvent(EngineeringEventType.RepositoryAnalysisCompleted, new { Repository = repositoryPath, SolutionCount = slnFiles.Count, ProjectCount = projFiles.Count }));

            await Task.CompletedTask;
        }
    }
}
